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
using System.ComponentModel;
using SlimDX;
using TerrainSample.World.Positionables;
using TerrainSample.World.Templates;

namespace TerrainSample.World.EntityComponents
{
    /// <summary>
    /// Controls how an <see cref="Entity{TCoordinates}"/> shall be rendered.
    /// </summary>
    /// <seealso cref="EntityTemplate.RenderControls"/>
    public abstract class RenderControl : ICloneable
    {
        /// <inheritdoc/>
        public override string ToString()
        {
            return GetType().Name;
        }

        /// <summary>
        /// How this component is to be shifted before rendering.
        /// </summary>
        [Description("How this component is to be shifted before rendering.")]
        public Vector3 Shift { get; set; }

        #region Clone
        /// <summary>
        /// Creates a shallow copy of this <see cref="RenderControl"/>
        /// </summary>
        /// <returns>The cloned <see cref="RenderControl"/>.</returns>
        public RenderControl Clone()
        {
            // Perform initial shallow copy
            return (RenderControl)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
        #endregion
    }
}
