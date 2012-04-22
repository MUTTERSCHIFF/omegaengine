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
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Common;
using Common.Values;
using Core;
using LuaInterface;
using OmegaEngine;
using OmegaEngine.Graphics;
using OmegaEngine.Graphics.Cameras;
using OmegaEngine.Graphics.Renderables;
using OmegaEngine.Graphics.Shaders;
using World;

namespace Presentation
{
    /// <summary>
    /// Handles the visual representation of <see cref="World"/> content in the <see cref="OmegaEngine"/>
    /// </summary>
    public abstract partial class Presenter : IDisposable
    {
        #region Variables
        /// <summary>
        /// The <see cref="Engine"/> reference to use for rendering operations
        /// </summary>
        protected readonly Engine Engine;

        /// <summary>
        /// Use lighting in this presentation?
        /// </summary>
        protected bool Lighting = true;

        private PostColorCorrectionShader _colorCorrectionShader;
        private PostBleachShader _bleachShader;

        /// <summary>
        /// The engine scene containing the graphical representations of <see cref="World.Positionable"/>s
        /// </summary>
        protected readonly Scene Scene;
        #endregion

        #region Properties
        /// <summary>
        /// Was <see cref="Initialize"/> already called?
        /// </summary>
        public bool Initialized { get; protected set; }

        private bool _wireframeEntities;

        /// <summary>
        /// Render all entities in wireframe-mode
        /// </summary>
        public bool WireframeEntities
        {
            get { return _wireframeEntities; }
            set
            {
                _wireframeEntities = value;
                foreach (var positionable in PositionableRenderables)
                    positionable.Wireframe = value;
            }
        }

        private bool _boundingSpheresEntities;

        /// <summary>
        /// Visualize the bounding spheres of all entities
        /// </summary>
        public bool BoundingSphereEntities
        {
            get { return _boundingSpheresEntities; }
            set
            {
                _boundingSpheresEntities = value;
                foreach (var positionable in PositionableRenderables)
                    positionable.DrawBoundingSphere = value;
            }
        }

        private bool _boundingBoxEntities;

        /// <summary>
        /// Visualize the bounding boxes of all entities
        /// </summary>
        public bool BoundingBoxEntities
        {
            get { return _boundingBoxEntities; }
            set
            {
                _boundingBoxEntities = value;
                foreach (var positionable in PositionableRenderables)
                    positionable.DrawBoundingBox = value;
            }
        }

        /// <summary>
        /// The universe data for this scene
        /// </summary>
        [LuaHide]
        public Universe Universe { get; protected set; }

        /// <summary>
        /// The engine view used to display the <see cref="Scene"/>
        /// </summary>
        public View View { get; protected set; }

        /// <summary>
        /// Was this presenter already disposed?
        /// </summary>
        [Browsable(false)]
        public bool Disposed { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new presenter
        /// </summary>
        /// <param name="engine">The engine to use for rendering</param>
        /// <param name="universe">The universe to display</param>
        protected Presenter(Engine engine, Universe universe)
        {
            #region Sanity checks
            if (engine == null) throw new ArgumentNullException("engine");
            if (universe == null) throw new ArgumentNullException("universe");
            #endregion

            Engine = engine;
            Universe = universe;
            Scene = new Scene(engine);
        }
        #endregion

        //--------------------//

        #region Engine Hook-in
        /// <summary>
        /// Hooks the <see cref="View"/>s into <see cref="OmegaEngine.Engine.Views"/>
        /// </summary>
        /// <remarks>Will internally call <see cref="Initialize"/> first, if you didn't</remarks>
        public virtual void HookIn()
        {
            #region Sanity checks
            if (Disposed) throw new ObjectDisposedException(ToString());
            if (!Initialized) Initialize();
            #endregion

            // Hook into engine
            Engine.Views.Add(View);
        }

        /// <summary>
        /// Hooks the <see cref="View"/>s out of <see cref="OmegaEngine.Engine.Views"/>
        /// </summary>
        public virtual void HookOut()
        {
            // Hook out of engine
            Engine.Views.Remove(View);
        }
        #endregion

        #region Camera
        /// <summary>
        /// Creates a new camera based on a state usually loaded from the <see cref="Universe"/>.
        /// </summary>
        /// <param name="state">The state to place the new camera in; may be <see langword="null"/> in which case it will default to looking at the center of the terrain.</param>
        /// <returns>The newly created <see cref="Camera"/>.</returns>
        public Camera CreateCamera(CameraState state)
        {
            if (state == null)
                state = new CameraState {Name = "Main", Position = new DoubleVector3(), Radius = 1500};

            const float minCameraRadius = 150, maxCameraRadius = 10000;

            return new TrackCamera(minCameraRadius, maxCameraRadius)
            {
                Name = state.Name,
                Target = state.Position,
                Radius = state.Radius,
                HorizontalRotation = state.Rotation,
                FarClip = 1e+6f
            };
        }

        /// <summary>
        /// Retreives the current state of the <see cref="Camera"/> for storage in the <see cref="Universe"/>.
        /// </summary>
        /// <returns>The current state of  the <see cref="Camera"/> or <see langword="null"/> if it can not be determined at this time (e.g. cinematic animation in progress).</returns>
        public CameraState CameraState
        {
            get
            {
                var camera = View.Camera as StrategyCamera;
                if (camera == null) return null;

                return new CameraState
                {
                    Name = camera.Name,
                    Position = camera.Target,
                    Radius = (float)camera.Radius,
                    Rotation = camera.HorizontalRotation
                };
            }
        }
        #endregion

        #region Dimming
        /// <summary>
        /// Dims in the screen down (and applies a <see cref="PostBleachShader"/> effect if possible)
        /// </summary>
        public void DimDown()
        {
            #region Sanity checks
            if (Disposed) throw new ObjectDisposedException(ToString());
            #endregion

            // Make sure the bleach effect is available and not already active
            if (_bleachShader != null)
            {
                // Gradually apply bleach effect
                _bleachShader.Enabled = true;
                Engine.Interpolate(0, 0.6, 4, true, value => _bleachShader.Opacity = (float)value);
            }

            // Dim down screen
            Engine.DimDown();
        }

        /// <summary>
        /// Dims in the screen back up (and removes the <see cref="PostBleachShader"/> effect)
        /// </summary>
        public void DimUp()
        {
            #region Sanity checks
            if (Disposed) throw new ObjectDisposedException(ToString());
            #endregion

            _bleachShader.Enabled = false;

            // Dim screen back up
            Engine.DimUp();
        }
        #endregion

        #region Music theme
        /// <summary>
        /// Switches the theme of the music played
        /// </summary>
        /// <param name="theme">The new music theme</param>
        /// <param name="immediate">Shall the current song be stopped and the new theme activated immediately?</param>
        protected void SwitchMusicTheme(string theme, bool immediate)
        {
            if (Settings.Current.Sound.PlayMusic && immediate)
            { // Change the music-playback bow
                Engine.Music.PlayTheme(theme);
            }
            else
            { // Change the next kind of music to be started by Game.Render() calling Engine.Music.Update()
                Engine.Music.SwitchTheme(theme);
            }
        }
        #endregion

        //--------------------//

        #region Dispose
        /// <summary>
        /// Removes the <see cref="Universe"/> hooks setup by <see cref="Initialize"/> and disposes all created <see cref="View"/>s, <see cref="Scene"/>s, <see cref="PositionableRenderable"/>s, etc.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        ~Presenter()
        {
            Dispose(false);
        }

        /// <summary>
        /// To be called by <see cref="IDisposable.Dispose"/> and the object destructor.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called manually and not by the garbage collector.</param>
        [SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations", Justification = "Only for debugging, not present in Release code")]
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed) return; // Don't try to dispose more than once

            // Remove event handlers watching the universe
            Universe.SkyboxChanged -= UpdateSykbox;
            Universe.Positionables.Added -= AddPositionable;
            Universe.Positionables.Removing -= RemovePositionable;

            if (disposing)
            { // This block will only be executed on manual disposal, not by Garbage Collection
                Log.Info("Disposing presenter");

                // Clean up assocs
                foreach (var entity in Universe.Positionables)
                {
                    try
                    {
                        RemovePositionable(entity);
                    }
                    catch (KeyNotFoundException)
                    {}
                }

                #region Sanity checks
                if (_worldToEngine.Count != 0)
                    throw new InvalidOperationException("Render associations left over after hook out");
                if (_engineToWorld.Count != 0)
                    throw new InvalidOperationException("Entity associations left over after hook out");
                #endregion

                // Dispose Engine-related data structures
                if (_colorCorrectionShader != null) _colorCorrectionShader.Dispose();
                if (_bleachShader != null) _bleachShader.Dispose();
                if (View != null) View.Dispose();
                if (Scene != null) Scene.Dispose();
            }
            else
            { // This block will only be executed on Garbage Collection, not by manual disposal
                Log.Error("Forgot to call Dispose on " + this);
#if DEBUG
                throw new InvalidOperationException("Forgot to call Dispose on " + this);
#endif
            }

            Disposed = true;
        }
        #endregion
    }
}