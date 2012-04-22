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
using Common.Values;
using SlimDX;
using OmegaEngine;
using OmegaEngine.Assets;
using World;
using EngineRenderable = OmegaEngine.Graphics.Renderables;
using View = OmegaEngine.Graphics.View;

namespace Presentation
{
    /// <summary>
    /// Displays a map for editing
    /// </summary>
    public sealed class EditorPresenter : InteractivePresenter
    {
        #region Constructor
        /// <summary>
        /// Creates a new editor presenter
        /// </summary>
        /// <param name="engine">The engine to use for rendering</param>
        /// <param name="universe">The universe to display</param>
        /// <param name="lighting">Shall lighting be used for rendering?</param>
        public EditorPresenter(Engine engine, Universe universe, bool lighting) : base(engine, universe)
        {
            #region Sanity checks
            if (engine == null) throw new ArgumentNullException("engine");
            if (universe == null) throw new ArgumentNullException("universe");
            #endregion

            Lighting = lighting;

            // Restore previous camera position (or default to center of terrain)
            var mainCamera = CreateCamera(universe.Camera);

            View = new View(engine, Scene, mainCamera) {Name = "Editor"};

            // Floating axis-arrows for easier orientation
            var axisArrows = new EngineRenderable.FloatingModel(engine, XMesh.Get(engine, "Engine/AxisArrows.x"))
            {Name = "AxisArrows", Alpha = 160, Position = new DoubleVector3(-16, -12, 40), Rotation = Quaternion.RotationYawPitchRoll(0, 0, 0)};
            axisArrows.SetScale(0.03f);
            View.FloatingModels.Add(axisArrows);
        }
        #endregion
    }
}