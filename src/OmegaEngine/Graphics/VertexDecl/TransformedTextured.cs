/*
 * Copyright 2006-2014 Bastian Eicher
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this file,
 * You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.Runtime.InteropServices;
using SlimDX;
using SlimDX.Direct3D9;

namespace OmegaEngine.Graphics.VertexDecl
{
    /// <summary>
    /// A fixed-function vertex format that stores a transformed position and texture coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TransformedTextured
    {
        #region Constants
        /// <summary>
        /// The fixed-function format of this vertex structure.
        /// </summary>
        public const VertexFormat Format = VertexFormat.PositionRhw | VertexFormat.Texture1;

        /// <summary>
        /// The length of this vertex structure in bytes.
        /// </summary>
        public const int StrideSize = 6 * 4;
        #endregion

        #region Variables
        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable FieldCanBeMadeReadOnly.Global
        /// <summary>The position of the vertex in screen-space</summary>
        public Vector3 Position;

        /// <summary>The reciprocal of homogeneous W (the depth-value)</summary>
        public float Rhw;

        /// <summary>The U-component of the texture coordinates</summary>
        public float Tu;

        /// <summary>The V-component of the texture coordinates</summary>
        public float Tv;

        // ReSharper restore FieldCanBeMadeReadOnly.Global
        // ReSharper restore MemberCanBePrivate.Global
        #endregion

        #region Properties
        /// <summary>The X-component of the position of the vertex in screen-space</summary>
        public float X { get { return Position.X; } set { Position.X = value; } }

        /// <summary>The Y-component of the position of the vertex in screen-space</summary>
        public float Y { get { return Position.Y; } set { Position.Y = value; } }

        /// <summary>The Z-component of the position of the vertex in screen-space</summary>
        public float Z { get { return Position.Z; } set { Position.Z = value; } }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new transformed, textured vertex
        /// </summary>
        /// <param name="position">The position of the vertex in screen-space</param>
        /// <param name="rhw">The reciprocal of homogeneous W (the depth-value)</param>
        /// <param name="tu">The U-component of the texture coordinates</param>
        /// <param name="tv">The V-component of the texture coordinates</param>
        public TransformedTextured(Vector3 position, float rhw, float tu, float tv)
        {
            Position = position;
            Rhw = rhw;
            Tu = tu;
            Tv = tv;
        }

        /// <summary>
        /// Creates a new transformed, textured vertex
        /// </summary>
        /// <param name="xvalue">The X-component of the position of the vertex in screen-space</param>
        /// <param name="yvalue">The Y-component of the position of the vertex in screen-space</param>
        /// <param name="zvalue">The Z-component of the position of the vertex in screen-space</param>
        /// <param name="rhw">The reciprocal of homogeneous W (the depth-value)</param>
        /// <param name="tu">The U-component of the texture coordinates</param>
        /// <param name="tv">The V-component of the texture coordinates</param>
        public TransformedTextured(float xvalue, float yvalue, float zvalue, float rhw, float tu, float tv)
            : this(new Vector3(xvalue, yvalue, zvalue), rhw, tu, tv)
        {}
        #endregion

        #region ToString
        public override string ToString()
        {
            return "TransformedTextured(position=" + Position + ", rhw=" + Rhw + ", " +
                   "tu=" + Tu + ", " + "tv=" + Tv + ")";
        }
        #endregion
    }
}
