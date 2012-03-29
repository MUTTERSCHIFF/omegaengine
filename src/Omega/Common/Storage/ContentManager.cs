/*
 * Copyright 2006-2012 Bastian Eicher
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
using System.IO;
using Common.Collections;
using Common.Streams;
using Common.Utils;
using Common.Properties;
using ICSharpCode.SharpZipLib.Zip;

namespace Common.Storage
{
    /// <summary>
    /// Provides a virtual file system for combining data from multiple directories and archives (useful for modding).
    /// </summary>
    public static class ContentManager
    {
        #region Constants
        /// <summary>
        /// The file extensions of content archives.
        /// </summary>
        public const string FileExt = ".pk5";
        #endregion

        #region Variables
        private static DirectoryInfo _baseDir, _modDir;
        private static List<ZipFile> _baseArchives, _modArchives;

        private static readonly Dictionary<string, ContentArchiveEntry>
            _baseArchiveData = new Dictionary<string, ContentArchiveEntry>(StringComparer.OrdinalIgnoreCase),
            _modArchiveData = new Dictionary<string, ContentArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region Properties
        /// <summary>
        /// The base directory where all the content files are stored; should not be <see langword="null"/>.
        /// </summary>
        /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory could not be found.</exception>
        public static DirectoryInfo BaseDir
        {
            get { return _baseDir; }
            set
            {
                if (value != null && !value.Exists)
                    throw new DirectoryNotFoundException(Resources.NotFoundGameDataDir + "\n" + value.FullName);
                _baseDir = value;
            }
        }

        /// <summary>
        /// A directory overriding the base directory for creating mods; may be <see langword="null"/>.
        /// </summary>
        /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory could not be found.</exception>
        public static DirectoryInfo ModDir
        {
            get { return _modDir; }
            set
            {
                if (value != null && !value.Exists)
                    throw new DirectoryNotFoundException(Resources.NotFoundModDataDir + "\n" + value.FullName);
                _modDir = value;
            }
        }
        #endregion

        //--------------------//

        #region Load archives
        /// <summary>
        /// Loads content archives from the base directory into the <see cref="ContentManager"/>.
        /// </summary>
        public static void LoadArchives()
        {
            if (_baseArchives != null)
                throw new InvalidOperationException(Resources.ContentArchivesAlreadyLoaded);

            #region Base files
            FileInfo[] baseFiles = BaseDir.GetFiles("*" + FileExt);
            _baseArchives = new List<ZipFile>(baseFiles.Length); // Use exact size for list capacity
            foreach (FileInfo file in baseFiles)
            {
                Log.Info("Load base data archive: " + file.Name);
                var zipFile = new ZipFile(file.FullName);
                foreach (ZipEntry zipEntry in zipFile)
                {
                    if (zipEntry.IsFile)
                    {
                        // Unify directory directory separator character
                        string filename = FileUtils.UnifySlashes(zipEntry.Name);

                        // Overwrite existing entries
                        if (_baseArchiveData.ContainsKey(filename)) _baseArchiveData.Remove(filename);
                        _baseArchiveData.Add(filename, new ContentArchiveEntry(zipFile, zipEntry));
                    }
                }
                _baseArchives.Add(zipFile);
            }
            #endregion

            #region Mod files
            if (ModDir != null)
            {
                FileInfo[] modFiles = ModDir.GetFiles("*" + FileExt);
                _modArchives = new List<ZipFile>(modFiles.Length); // Use exact size for list capacity
                foreach (FileInfo file in modFiles)
                {
                    Log.Info("Load mod data archive: " + file.Name);
                    var zipFile = new ZipFile(file.FullName);
                    foreach (ZipEntry zipEntry in zipFile)
                    {
                        if (zipEntry.IsFile)
                        {
                            // Unify directory directory separator character
                            string filename = FileUtils.UnifySlashes(zipEntry.Name);

                            // Overwrite existing entries
                            if (_modArchiveData.ContainsKey(filename)) _modArchiveData.Remove(filename);
                            _modArchiveData.Add(filename, new ContentArchiveEntry(zipFile, zipEntry));
                        }
                    }
                    _modArchives.Add(zipFile);
                }
            }
            #endregion
        }
        #endregion

        #region Close archives
        /// <summary>
        /// Closes the content archives loaded into the <see cref="ContentManager"/>.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Errors on shutdown because of an inconsistent state are useless and annoying")]
        public static void CloseArchives()
        {
            #region Base files
            if (_baseArchives != null)
            {
                foreach (ZipFile zipFile in _baseArchives)
                {
                    Log.Info("Close base data archive: " + zipFile.Name);
                    try
                    {
                        zipFile.Close();
                    }
                        // ReSharper disable EmptyGeneralCatchClause
                    catch
                    {}
                    // ReSharper restore EmptyGeneralCatchClause
                }
                _baseArchives = null;
                _baseArchiveData.Clear();
            }
            #endregion

            #region Mod files
            if (_modArchives != null)
            {
                foreach (ZipFile zipFile in _modArchives)
                {
                    Log.Info("Close mod data archive: " + zipFile.Name);
                    try
                    {
                        zipFile.Close();
                    }
                        // ReSharper disable EmptyGeneralCatchClause
                    catch
                    {}
                    // ReSharper restore EmptyGeneralCatchClause
                }
                _modArchives = null;
                _modArchiveData.Clear();
            }
            #endregion
        }
        #endregion

        //--------------------//

        #region Create directory path
        /// <summary>
        /// Creates a path for a content directory (using the <see cref="ModDir"/> if available).
        /// </summary>
        /// <param name="type">The type of file (e.g. Textures, Sounds, ...).</param>
        /// <returns>The absolute path to the requested directory.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory could not be found.</exception>
        public static string CreateDirPath(string type)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException("type");
            #endregion

            // Unify directory directory separator character
            type = FileUtils.UnifySlashes(type);

            // Use mod directory if available
            string pathBase;
            if (ModDir != null) pathBase = ModDir.FullName;
            else if (BaseDir != null) pathBase = _baseDir.FullName;
            else throw new DirectoryNotFoundException(Resources.NotFoundGameDataDir + "\n-");

            // Check the path before returning it
            var directory = new DirectoryInfo(Path.Combine(pathBase, type));
            if (!directory.Exists) directory.Create();
            return directory.FullName;
        }
        #endregion

        #region Create file path
        /// <summary>
        /// Creates a path for a content file (using <see cref="ModDir"/> if available).
        /// </summary>
        /// <param name="type">The type of file (e.g. Textures, Sounds, ...).</param>
        /// <param name="id">The file name of the content.</param>
        /// <returns>The absolute path to the requested content file.</returns>
        public static string CreateFilePath(string type, string id)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException("type");
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException("id");
            #endregion

            // Unify directory directory separator character
            type = FileUtils.UnifySlashes(type);
            id = FileUtils.UnifySlashes(id);

            return Path.Combine(CreateDirPath(type), id);
        }
        #endregion

        #region File exists
        /// <summary>
        /// Checks whether a certain content file exists.
        /// </summary>
        /// <param name="type">The type of file (e.g. Textures, Sounds, ...).</param>
        /// <param name="id">The file name of the content.</param>
        /// <param name="searchArchives">Whether to search for the file in archives as well.</param>
        /// <returns><see langword="true"/> if the requested content file exists.</returns>
        public static bool FileExists(string type, string id, bool searchArchives)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException("type");
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException("id");
            #endregion

            // Unify directory directory separator character
            type = FileUtils.UnifySlashes(type);
            id = FileUtils.UnifySlashes(id);

            string fullID = Path.Combine(type, id);

            if (ModDir != null && File.Exists(Path.Combine(ModDir.FullName, fullID)))
                return true;
            if (BaseDir != null && File.Exists(Path.Combine(BaseDir.FullName, fullID)))
                return true;
            return searchArchives && _baseArchiveData.ContainsKey(fullID);
        }
        #endregion

        #region Get file list

        #region Helpers
        /// <summary>
        /// Adds a specific file to the <paramref name="files"/> list.
        /// </summary>
        /// <param name="files">The collection to add the file to.</param>
        /// <param name="type">The type-subdirectory the file belongs to.</param>
        /// <param name="name">The file name to be added to the list.</param>
        /// <param name="flagAsMod">Set to <see langword="true"/> when handling mod files to detect added and changed files.</param>
        private static void AddFileToList(INamedCollection<FileEntry> files, string type, string name, bool flagAsMod)
        {
            if (flagAsMod)
            {
                // Detect whether this is a new file or a replacement for an existing one
                if (files.Contains(name))
                {
                    var previousEntry = files[name];

                    // Only mark as modified if the pre-existing file isn't already a mod file itself
                    if (previousEntry.EntryType == FileEntryType.Normal)
                    {
                        files.Remove(previousEntry);
                        files.Add(new FileEntry(type, name, FileEntryType.Modified));
                    }
                }
                else files.Add(new FileEntry(type, name, FileEntryType.Added));
            }
            else
            {
                // Prevent duplicate entries
                if (!files.Contains(name)) files.Add(new FileEntry(type, name));
            }
        }

        /// <summary>
        /// Recursively finds all files in <paramref name="directory"/> ending with <paramref name="extension"/> and adds them to the <paramref name="files"/> list.
        /// </summary>
        /// <param name="files">The collection to add the found files to.</param>
        /// <param name="type">The type-subdirectory the files belong to.</param>
        /// <param name="extension">The file extension to look for.</param>
        /// <param name="directory">The directory to look in.</param>
        /// <param name="prefix">A prefix to add before the file name in the list (used to indicate current sub-directory).</param>
        /// <param name="flagAsMod">Set to <see langword="true"/> when handling mod files to detect added and changed files.</param>
        private static void AddDirectoryToList(INamedCollection<FileEntry> files, string type, string extension, DirectoryInfo directory, string prefix, bool flagAsMod)
        {
            // Add the files in this directory to the list
            foreach (FileInfo file in directory.GetFiles("*" + extension))
                AddFileToList(files, type, prefix + file.Name, flagAsMod);

            // Recursively call this method for all sub-directories
            foreach (DirectoryInfo subDir in directory.GetDirectories())
            {
                // Don't add dot directories (e.g. .svn)
                if (subDir.Name.StartsWith(".")) continue;

                AddDirectoryToList(files, type, extension, subDir, prefix + subDir.Name + Path.DirectorySeparatorChar, flagAsMod);
            }
        }

        /// <summary>
        /// Finds all files in <paramref name="archiveData"/> ending with <paramref name="extension"/> and adds them to the <paramref name="files"/> collection
        /// </summary>
        /// <param name="files">The collection to add the found files to.</param>
        /// <param name="extension">The file extension to look for.</param>
        /// <param name="type">The type-subdirectory to look in.</param>
        /// <param name="archiveData">The archive data list to look in.</param>
        /// <param name="flagAsMod">Set to <see langword="true"/> when handling mod files to detect added and changed files.</param>
        private static void AddArchivesToList(INamedCollection<FileEntry> files, string type, string extension, IEnumerable<KeyValuePair<string, ContentArchiveEntry>> archiveData, bool flagAsMod)
        {
            foreach (var pair in archiveData)
            {
                if (pair.Key.StartsWith(type, StringComparison.OrdinalIgnoreCase) && pair.Key.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    // Cut away the type part of the path
                    AddFileToList(files, type, pair.Key.Substring(type.Length + 1), flagAsMod);
                }
            }
        }
        #endregion

        /// <summary>
        /// Gets a list of all files of a certain type
        /// </summary>
        /// <param name="type">The type of files you want (e.g. Textures, Sounds, ...)</param>
        /// <param name="extension">The file extension to so search for</param>
        /// <returns>An collection of strings with file IDs</returns>
        public static INamedCollection<FileEntry> GetFileList(string type, string extension)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException("type");
            if (string.IsNullOrEmpty(extension)) throw new ArgumentNullException("extension");
            #endregion

            // Unify directory directory separator character
            type = FileUtils.UnifySlashes(type);

            // Create an alphabetical list of files without duplicates
            var files = new NamedCollection<FileEntry>();

            #region Find all base files
            // Find real files
            if (Directory.Exists(Path.Combine(BaseDir.FullName, type)))
            {
                AddDirectoryToList(files, type, extension,
                    new DirectoryInfo(Path.Combine(BaseDir.FullName, type)), "", false);
            }

            // Find files in archives
            AddArchivesToList(files, type, extension, _baseArchiveData, false);
            #endregion

            if (ModDir != null)
            {
                #region Find all mod files
                // Find real files
                if (Directory.Exists(Path.Combine(ModDir.FullName, type)))
                {
                    AddDirectoryToList(files, type, extension,
                        new DirectoryInfo(Path.Combine(ModDir.FullName, type)), "", true);
                }

                // Find files in archives
                AddArchivesToList(files, type, extension, _modArchiveData, true);
                #endregion
            }

            return files;
        }
        #endregion

        #region Get file path
        /// <summary>
        /// Gets the file path for a content file (does not search in archives)
        /// </summary>
        /// <param name="type">The type of file (e.g. Textures, Sounds, ...).</param>
        /// <param name="id">The file name of the content.</param>
        /// <returns>The absolute path to the requested content file</returns>
        /// <exception cref="FileNotFoundException">Thrown if the specified file could not be found.</exception>
        public static string GetFilePath(string type, string id)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException("type");
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException("id");
            #endregion

            // Unify directory directory separator character
            type = FileUtils.UnifySlashes(type);
            id = FileUtils.UnifySlashes(id);

            string path;

            if (ModDir != null)
            {
                path = Path.Combine(ModDir.FullName, Path.Combine(type, id));
                if (File.Exists(path)) return path;
            }

            if (BaseDir != null)
            {
                path = Path.Combine(BaseDir.FullName, Path.Combine(type, id));
                if (File.Exists(path)) return path;
            }

            throw new FileNotFoundException(Resources.NotFoundGameContentFile + "\n" + Path.Combine(type, id), Path.Combine(type, id));
        }
        #endregion

        #region Get file stream
        /// <summary>
        /// Gets a reading stream for a content file (searches in archives)
        /// </summary>
        /// <param name="type">The type of file (e.g. Textures, Sounds, ...).</param>
        /// <param name="id">The file name of the content.</param>
        /// <returns>The absolute path to the requested content file</returns>
        /// <exception cref="FileNotFoundException">Thrown if the specified file could not be found.</exception>
        /// <exception cref="IOException">Thrown if there was an error reading the file.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if read access to the file is not permitted.</exception>
        public static Stream GetFileStream(string type, string id)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException("type");
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException("id");
            #endregion

            // Unify directory directory separator character
            type = FileUtils.UnifySlashes(type);
            id = FileUtils.UnifySlashes(id);

            // First try to load a real file
            if (FileExists(type, id, false))
                return File.OpenRead(GetFilePath(type, id));

            // Then look in the archives
            string fullID = Path.Combine(type, id);

            #region Mod
            if (ModDir != null)
            {
                // Real file
                string path = Path.Combine(ModDir.FullName, fullID);
                if (File.Exists(path)) return File.OpenRead(path);

                // Archive entry
                if (_modArchiveData.ContainsKey(fullID))
                {
                    // Copy from ZIP file to MemoryStream to provide seeking capability
                    Stream memoryStream = new MemoryStream();
                    try
                    {
                        using (var inputStream = _modArchiveData[fullID].ZipFile.GetInputStream(_modArchiveData[fullID].ZipEntry))
                            StreamUtils.Copy(inputStream, memoryStream);
                    }
                        #region Error handling
                    catch (ZipException ex)
                    {
                        throw new IOException(ex.Message, ex);
                    }
                    #endregion

                    return memoryStream;
                }
            }
            #endregion

            #region Base
            if (BaseDir != null)
            {
                // Real file
                string path = Path.Combine(BaseDir.FullName, fullID);
                if (File.Exists(path)) return File.OpenRead(path);

                // Archive entry
                if (_baseArchiveData.ContainsKey(fullID))
                {
                    // Copy from ZIP file to MemoryStream to provide seeking capability
                    Stream memoryStream = new MemoryStream();
                    using (var inputStream = _baseArchiveData[fullID].ZipFile.GetInputStream(_baseArchiveData[fullID].ZipEntry))
                        StreamUtils.Copy(inputStream, memoryStream);
                    return memoryStream;
                }
            }
            #endregion

            throw new FileNotFoundException(Resources.NotFoundGameContentFile + "\n" + Path.Combine(type, id), Path.Combine(type, id));
        }
        #endregion

        #region Delete mod file
        /// <summary>
        /// Deletes a file in <see cref="ModDir"/>. Will not touch files in archives or in <see cref="BaseDir"/>.
        /// </summary>
        /// <param name="type">The type of file (e.g. Textures, Sounds, ...).</param>
        /// <param name="id">The file name of the content.</param>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="ModDir"/> is not set.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the specified file could not be found.</exception>
        /// <exception cref="IOException">Thrown if the specified file could not be deleted.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if you have insufficient rights to delete the file.</exception>
        public static void DeleteModFile(string type, string id)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException("type");
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException("id");
            #endregion

            // Ensure there is an active mod
            if (ModDir == null) throw new InvalidOperationException(Resources.NoModActive);

            // Try to delete a file in that mod
            File.Delete(Path.Combine(ModDir.FullName, Path.Combine(type, id)));
        }
        #endregion
    }
}
