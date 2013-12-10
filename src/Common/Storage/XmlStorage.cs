/*
 * Copyright 2006-2013 Bastian Eicher
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Common.Utils;
using Common.Properties;
using ICSharpCode.SharpZipLib.Zip;

namespace Common.Storage
{
    /// <summary>
    /// Provides easy serialization to XML files (optionally wrapped in ZIP archives).
    /// </summary>
    /// <remarks>This class only serializes public properties.</remarks>
    public static class XmlStorage
    {
        #region Constants
        /// <summary>
        /// The XML namespace used for XML Schema instance.
        /// </summary>
        public const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
        #endregion

        #region Serializer generation
        /// <summary>An internal cache of XML serializers identified by the target type and ignored sub-types.</summary>
        private static readonly Dictionary<string, XmlSerializer> _serializers = new Dictionary<string, XmlSerializer>();

        /// <summary>Used to mark something as "serialize as XML attribute".</summary>
        private static readonly XmlAttributes _asAttribute = new XmlAttributes {XmlAttribute = new XmlAttributeAttribute()};

        /// <summary>Used to mark something to be ignored when serializing.</summary>
        private static readonly XmlAttributes _ignore = new XmlAttributes {XmlIgnore = true};

        /// <summary>
        /// Gets a <see cref="XmlSerializer"/> for classes of the type <paramref name="type"/>. Results are cached internally.
        /// </summary>
        /// <param name="type">The type to get the serializer for.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <returns>The cached or newly created <see cref="XmlSerializer"/>.</returns>
        /// <remarks>Can't use .NET's built-in <see cref="XmlSerializer"/>-caching because we apply custom overrides.</remarks>
        private static XmlSerializer GetSerializer(Type type, IEnumerable<MemberInfo> ignoreMembers)
        {
            // Create a string key containing the type name and optionally the ignore-type names
            string key = type.FullName;
            if (ignoreMembers != null)
            {
                key = ignoreMembers.Where(ignoreMember => ignoreMember != null).
                    Aggregate(key, (current, ignoreMember) => current + (" \\ " + ignoreMember.ReflectedType.FullName + ignoreMember.Name));
            }

            XmlSerializer serializer;
            // Try to find a suitable serializer in the cache
            if (!_serializers.TryGetValue(key, out serializer))
            {
                // Create a new serializer and add it to the cache
                serializer = CreateSerializer(type, ignoreMembers);
                _serializers.Add(key, serializer);
            }

            return serializer;
        }

        /// <summary>
        /// Creates a new <see cref="XmlSerializer"/> for the type <paramref name="type"/> and applies a set of default augmentations for .NET types.
        /// </summary>
        /// <param name="type">The type to create the serializer for.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <returns>The newly created <see cref="XmlSerializer"/>.</returns>
        /// <remarks>This method may be rather slow, so its results should be cached.</remarks>
        private static XmlSerializer CreateSerializer(Type type, IEnumerable<MemberInfo> ignoreMembers)
        {
            var overrides = new XmlAttributeOverrides();

            #region Augment .NET BCL types
            MembersAsAttributes<Point>(overrides, "X", "Y");
            MembersAsAttributes<Size>(overrides, "Width", "Height");
            MembersAsAttributes<Rectangle>(overrides, "X", "Y", "Width", "Height");
            overrides.Add(typeof(Rectangle), "Location", _ignore);
            overrides.Add(typeof(Rectangle), "Size", _ignore);
            overrides.Add(typeof(Exception), "Data", _ignore);
            #endregion

            #region Augement SlimDX types
            MembersAsAttributes<SlimDX.Color3>(overrides, "Red", "Green", "Blue");
            MembersAsAttributes<SlimDX.Color4>(overrides, "Alpha", "Red", "Green", "Blue");
            MembersAsAttributes<SlimDX.Half2>(overrides, "X", "Y");
            MembersAsAttributes<SlimDX.Half3>(overrides, "X", "Y", "Z");
            MembersAsAttributes<SlimDX.Half4>(overrides, "X", "Y", "Z", "W");
            MembersAsAttributes<SlimDX.Vector2>(overrides, "X", "Y");
            MembersAsAttributes<SlimDX.Vector3>(overrides, "X", "Y", "Z");
            MembersAsAttributes<SlimDX.Vector4>(overrides, "X", "Y", "Z", "W");
            MembersAsAttributes<SlimDX.Quaternion>(overrides, "X", "Y", "Z", "W");
            MembersAsAttributes<SlimDX.Rational>(overrides, "Numerator", "Denominator");
            #endregion

            // Ignore specific fields)
            if (ignoreMembers != null)
            {
                foreach (MemberInfo ignoreMember in ignoreMembers.Where(ignoreMember => ignoreMember != null))
                    overrides.Add(ignoreMember.ReflectedType, ignoreMember.Name, _ignore);
            }

            var serializer = new XmlSerializer(type, overrides);
            serializer.UnknownAttribute += (sender, e) => Log.Warn("Ignored XML attribute while deserializing: " + e.Attr);
            serializer.UnknownElement += (sender, e) => Log.Warn("Ignored XML element while deserializing: " + e.Element);
            return serializer;
        }

        /// <summary>
        /// Configures a set of members of a type to be serialized as XML attributes.
        /// </summary>
        private static void MembersAsAttributes<T>(XmlAttributeOverrides overrides, params string[] members)
        {
            Type type = typeof(T);
            foreach (string memeber in members)
                overrides.Add(type, memeber, _asAttribute);
        }
        #endregion

        //--------------------//

        #region Load plain
        /// <summary>
        /// Loads an object from an XML file.
        /// </summary>
        /// <typeparam name="T">The type of object the XML stream shall be converted into.</typeparam>
        /// <param name="stream">The stream to read the encoded XML data from.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <returns>The loaded object.</returns>
        /// <exception cref="InvalidDataException">Thrown if a problem occurred while deserializing the XML data.</exception>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "The type parameter is used to determine the type of returned object")]
        public static T LoadXml<T>(Stream stream, params MemberInfo[] ignoreMembers)
        {
            #region Sanity checks
            if (stream == null) throw new ArgumentNullException("stream");
            #endregion

            if (stream.CanSeek) stream.Position = 0;
            try
            {
                return (T)GetSerializer(typeof(T), ignoreMembers).Deserialize(stream);
            }
                #region Error handling
            catch (InvalidOperationException ex)
            {
                // Convert exception type
                throw new InvalidDataException(ex.Message, ex.InnerException) {Source = ex.Source};
            }
            #endregion
        }

        /// <summary>
        /// Loads an object from an XML file.
        /// </summary>
        /// <typeparam name="T">The type of object the XML stream shall be converted into.</typeparam>
        /// <param name="path">The path of the file to load.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <returns>The loaded object.</returns>
        /// <exception cref="IOException">Thrown if a problem occurred while reading the file.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if read access to the file is not permitted.</exception>
        /// <exception cref="InvalidDataException">Thrown if a problem occurred while deserializing the XML data.</exception>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "The type parameter is used to determine the type of returned object")]
        public static T LoadXml<T>(string path, params MemberInfo[] ignoreMembers)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            #endregion

            try
            {
                using (var fileStream = File.OpenRead(path))
                    return LoadXml<T>(fileStream, ignoreMembers);
            }
                #region Error handling
            catch (InvalidDataException ex)
            {
                // Change exception message to add context information
                throw new InvalidDataException(string.Format(Resources.ProblemLoading, path) + "\n" + ex.Message, ex.InnerException);
            }
            #endregion
        }

        /// <summary>
        /// Loads an object from an XML string.
        /// </summary>
        /// <typeparam name="T">The type of object the XML stream shall be converted into.</typeparam>
        /// <param name="data">The XML string to be parsed.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <returns>The loaded object.</returns>
        /// <exception cref="InvalidDataException">Thrown if a problem occurred while deserializing the XML data.</exception>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "The type parameter is used to determine the type of returned object")]
        public static T FromXmlString<T>(string data, params MemberInfo[] ignoreMembers)
        {
            #region Sanity checks
            if (data == null) throw new ArgumentNullException("data");
            #endregion

            // Copy string to a stream and then parse
            using (var stream = data.ToStream())
                return LoadXml<T>(stream, ignoreMembers);
        }
        #endregion

        #region Save plain
        /// <summary>
        /// Saves an object in an XML stream ending with a line break.
        /// </summary>
        /// <typeparam name="T">The type of object to be saved in an XML stream.</typeparam>
        /// <param name="data">The object to be stored.</param>
        /// <param name="stream">The stream to write the encoded XML data to.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        public static void SaveXml<T>(this T data, Stream stream, params MemberInfo[] ignoreMembers)
        {
            #region Sanity checks
            if (stream == null) throw new ArgumentNullException("stream");
            #endregion

            var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings {Encoding = new UTF8Encoding(false), Indent = true, IndentChars = "\t", NewLineChars = "\n"});
            var serializer = GetSerializer(typeof(T), ignoreMembers);

            // Detect and handle namespace attributes
            var rootAttribute = AttributeUtils.GetAttributes<XmlRootAttribute, T>().FirstOrDefault();
            var namespaceAttributes = AttributeUtils.GetAttributes<XmlNamespaceAttribute, T>();
            var qualifiedNames = namespaceAttributes.Select(attr => attr.QualifiedName);
            if (rootAttribute != null) qualifiedNames = qualifiedNames.Concat(new[] {new XmlQualifiedName("", rootAttribute.Namespace)});

            if (qualifiedNames.Any()) serializer.Serialize(xmlWriter, data, new XmlSerializerNamespaces(qualifiedNames.ToArray()));
            else serializer.Serialize(xmlWriter, data);

            // End file with line break
            if (xmlWriter.Settings != null)
            {
                var newLine = xmlWriter.Settings.Encoding.GetBytes(xmlWriter.Settings.NewLineChars);
                stream.Write(newLine, 0, newLine.Length);
            }
        }

        /// <summary>
        /// Saves an object in an XML file ending with a line break.
        /// </summary>
        /// <remarks>This method performs an atomic write operation when possible.</remarks>
        /// <typeparam name="T">The type of object to be saved in an XML stream.</typeparam>
        /// <param name="data">The object to be stored.</param>
        /// <param name="path">The path of the file to write.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <exception cref="IOException">Thrown if a problem occurred while writing the file.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if write access to the file is not permitted.</exception>
        public static void SaveXml<T>(this T data, string path, params MemberInfo[] ignoreMembers)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            #endregion

            using (var atomic = new AtomicWrite(path))
            using (var fileStream = File.Create(atomic.WritePath))
            {
                SaveXml(data, fileStream, ignoreMembers);
                atomic.Commit();
            }
        }

        /// <summary>
        /// Returns an object as an XML string ending with a line break.
        /// </summary>
        /// <typeparam name="T">The type of object to be saved in an XML stream.</typeparam>
        /// <param name="data">The object to be stored.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <returns>A string containing the XML code.</returns>
        public static string ToXmlString<T>(this T data, params MemberInfo[] ignoreMembers)
        {
            using (var stream = new MemoryStream())
            {
                // Write to a memory stream
                SaveXml(data, stream, ignoreMembers);

                // Copy the stream to a string
                return stream.ReadToString();
            }
        }
        #endregion

        #region Stylesheet
        /// <summary>
        /// Adds an XSL stylesheet instruction to an existing XML file.
        /// </summary>
        /// <param name="path">The XML file to add the stylesheet instruction to.</param>
        /// <param name="stylesheetFile">The file name of the stylesheet to reference.</param>
        /// <exception cref="FileNotFoundException">Thrown if the XML file to add the stylesheet reference to could not be found.</exception>
        /// <exception cref="IOException">Thrown if the XML file to add the stylesheet reference to could not be read or written.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if read or write access to the XML file is not permitted.</exception>
        public static void AddStylesheet(string path, string stylesheetFile)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (string.IsNullOrEmpty(stylesheetFile)) throw new ArgumentNullException("stylesheetFile");
            #endregion

            // Loads the XML document
            var dom = new XmlDocument();
            dom.Load(path);

            // Adds a new XSL stylesheet instruction to the DOM
            var stylesheetInstruction = dom.CreateProcessingInstruction("xml-stylesheet", string.Format(CultureInfo.InvariantCulture, "type='text/xsl' href='{0}'", stylesheetFile));
            dom.InsertAfter(stylesheetInstruction, dom.FirstChild);

            // Writes back the modified XML document
            using (var xmlWriter = XmlWriter.Create(path, new XmlWriterSettings {Encoding = new UTF8Encoding(false), Indent = true, IndentChars = "\t", NewLineChars = "\n"}))
            {
                dom.WriteTo(xmlWriter);

                // End file with line break
                if (xmlWriter.Settings != null) xmlWriter.WriteWhitespace(xmlWriter.Settings.NewLineChars);
            }
        }
        #endregion

        //--------------------//

        #region Load ZIP
        /// <summary>
        /// Loads an object from an XML file embedded in a ZIP archive.
        /// </summary>
        /// <typeparam name="T">The type of object the XML stream shall be converted into.</typeparam>
        /// <param name="stream">The ZIP archive to load.</param>
        /// <param name="password">The password to use for decryption; <see langword="null"/> for no encryption.</param>
        /// <param name="additionalFiles">Additional files stored alongside the XML file in the ZIP archive to be read; may be <see langword="null"/>.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <returns>The loaded object.</returns>
        /// <exception cref="ZipException">Thrown if a problem occurred while reading the ZIP data or if <paramref name="password"/> is wrong.</exception>
        /// <exception cref="InvalidDataException">Thrown if a problem occurred while deserializing the XML data.</exception>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "The type parameter is used to determine the type of returned object")]
        public static T LoadXmlZip<T>(Stream stream, string password = null, IEnumerable<EmbeddedFile> additionalFiles = null, params MemberInfo[] ignoreMembers)
        {
            #region Sanity checks
            if (stream == null) throw new ArgumentNullException("stream");
            #endregion

            bool xmlFound = false;
            T output = default(T);

            using (var zipFile = new ZipFile(stream) {IsStreamOwner = false})
            {
                zipFile.Password = password;

                foreach (ZipEntry zipEntry in zipFile)
                {
                    if (StringUtils.EqualsIgnoreCase(zipEntry.Name, "data.xml"))
                    {
                        // Read the XML file from the ZIP archive
                        var inputStream = zipFile.GetInputStream(zipEntry);
                        output = LoadXml<T>(inputStream, ignoreMembers);
                        xmlFound = true;
                    }
                    else
                    {
                        if (additionalFiles != null)
                        {
                            // Read additional files from the ZIP archive
                            foreach (EmbeddedFile file in additionalFiles)
                            {
                                if (StringUtils.EqualsIgnoreCase(zipEntry.Name, file.Filename))
                                {
                                    var inputStream = zipFile.GetInputStream(zipEntry);
                                    file.StreamDelegate(inputStream);
                                }
                            }
                        }
                    }
                }
            }

            if (xmlFound) return output;
            throw new InvalidDataException(Resources.NoXmlDataInZip);
        }

        /// <summary>
        /// Loads an object from an XML file embedded in a ZIP archive.
        /// </summary>
        /// <typeparam name="T">The type of object the XML stream shall be converted into.</typeparam>
        /// <param name="path">The ZIP archive to load.</param>
        /// <param name="password">The password to use for decryption; <see langword="null"/> for no encryption.</param>
        /// <param name="additionalFiles">Additional files stored alongside the XML file in the ZIP archive to be read; may be <see langword="null"/>.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <returns>The loaded object.</returns>
        /// <exception cref="IOException">Thrown if a problem occurred while reading the file.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if read access to the file is not permitted.</exception>
        /// <exception cref="ZipException">Thrown if a problem occurred while reading the ZIP data or if <paramref name="password"/> is wrong.</exception>
        /// <exception cref="InvalidDataException">Thrown if a problem occurred while deserializing the XML data.</exception>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "The type parameter is used to determine the type of returned object")]
        public static T LoadXmlZip<T>(string path, string password = null, IEnumerable<EmbeddedFile> additionalFiles = null, params MemberInfo[] ignoreMembers)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            #endregion

            using (var fileStream = File.OpenRead(path))
                return LoadXmlZip<T>(fileStream, password, additionalFiles, ignoreMembers);
        }
        #endregion

        #region Save ZIP
        /// <summary>
        /// Saves an object in an XML file embedded in a ZIP archive.
        /// </summary>
        /// <typeparam name="T">The type of object to be saved in an XML stream.</typeparam>
        /// <param name="data">The object to be stored.</param>
        /// <param name="stream">The ZIP archive to be written.</param>
        /// <param name="password">The password to use for encryption; <see langword="null"/> for no encryption.</param>
        /// <param name="additionalFiles">Additional files to be stored alongside the XML file in the ZIP archive; may be <see langword="null"/>.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        public static void SaveXmlZip<T>(this T data, Stream stream, string password = null, IEnumerable<EmbeddedFile> additionalFiles = null, params MemberInfo[] ignoreMembers)
        {
            #region Sanity checks
            if (stream == null) throw new ArgumentNullException("stream");
            #endregion

            if (stream.CanSeek) stream.Position = 0;
            using (var zipStream = new ZipOutputStream(stream) {IsStreamOwner = false})
            {
                if (!string.IsNullOrEmpty(password)) zipStream.Password = password;

                // Write the XML file to the ZIP archive
                {
                    var entry = new ZipEntry("data.xml") {DateTime = DateTime.Now};
                    if (!string.IsNullOrEmpty(password)) entry.AESKeySize = 128;
                    zipStream.SetLevel(9);
                    zipStream.PutNextEntry(entry);
                    SaveXml(data, zipStream, ignoreMembers);
                    zipStream.CloseEntry();
                }

                // Write additional files to the ZIP archive
                if (additionalFiles != null)
                {
                    foreach (EmbeddedFile file in additionalFiles)
                    {
                        var entry = new ZipEntry(file.Filename) {DateTime = DateTime.Now};
                        if (!string.IsNullOrEmpty(password)) entry.AESKeySize = 128;
                        zipStream.SetLevel(file.CompressionLevel);
                        zipStream.PutNextEntry(entry);
                        file.StreamDelegate(zipStream);
                        zipStream.CloseEntry();
                    }
                }
            }
        }

        /// <summary>
        /// Saves an object in an XML file embedded in a ZIP archive.
        /// </summary>
        /// <typeparam name="T">The type of object to be saved in an XML stream.</typeparam>
        /// <param name="data">The object to be stored.</param>
        /// <param name="path">The ZIP archive to be written.</param>
        /// <param name="password">The password to use for encryption; <see langword="null"/> for no encryption.</param>
        /// <param name="additionalFiles">Additional files to be stored alongside the XML file in the ZIP archive; may be <see langword="null"/>.</param>
        /// <param name="ignoreMembers">Fields to be ignored when serializing.</param>
        /// <exception cref="IOException">Thrown if a problem occurred while writing the file.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if write access to the file is not permitted.</exception>
        public static void SaveXmlZip<T>(this T data, string path, string password = null, IEnumerable<EmbeddedFile> additionalFiles = null, params MemberInfo[] ignoreMembers)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            #endregion

            // Make sure the containing directory exists
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            // Prepend random string for temp file name
            string tempPath = directory + Path.DirectorySeparatorChar + "temp." + Path.GetRandomFileName() + "." + Path.GetFileName(path);

            try
            {
                // Write to temporary file first
                using (var fileStream = File.Create(tempPath))
                    SaveXmlZip(data, fileStream, password, additionalFiles, ignoreMembers);
                FileUtils.Replace(tempPath, path);
            }
                #region Error handling
            catch (Exception)
            {
                // Clean up failed transactions
                if (File.Exists(tempPath)) File.Delete(tempPath);
                throw;
            }
            #endregion
        }
        #endregion

        //--------------------//

        #region Embedded files
        /// <summary>
        /// Returns a stream containing a file embedded into a XML-ZIP archive.
        /// </summary>
        /// <param name="stream">The ZIP archive to be loaded.</param>
        /// <param name="password">The password to use for decryption; <see langword="null"/> for no encryption.</param>
        /// <param name="name">The name of the embedded file.</param>
        /// <returns>A stream containing the embedded file.</returns>
        /// <exception cref="IOException">Thrown if a problem occurred while reading the file.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if read access to the file is not permitted.</exception>
        /// <exception cref="ZipException">Thrown if a problem occurred while reading the ZIP data.</exception>
        public static Stream GetEmbeddedFileStream(Stream stream, string password, string name)
        {
            using (var zipFile = new ZipFile(stream))
            {
                zipFile.Password = password;
                return zipFile.GetInputStream(zipFile.GetEntry(name));
            }
        }
        #endregion
    }
}