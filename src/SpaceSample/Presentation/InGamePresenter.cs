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
using OmegaEngine;
using OmegaEngine.Graphics;
using World;

namespace Presentation
{
    /// <summary>
    /// Main in-game interaction
    /// </summary>
    public sealed class InGamePresenter : InteractivePresenter
    {
        #region Constructor
        /// <summary>
        /// Creates a new presenter for the actual running game
        /// </summary>
        /// <param name="engine">The engine to use for rendering</param>
        /// <param name="universe">The universe to display</param>
        public InGamePresenter(Engine engine, Universe universe) : base(engine, universe)
        {
            #region Sanity checks
            if (engine == null) throw new ArgumentNullException("engine");
            if (universe == null) throw new ArgumentNullException("universe");
            #endregion

            // Restore previous camera position (or default to center of terrain)
            var mainCamera = CreateCamera(universe.Camera);

            View = new View(engine, Scene, mainCamera) {Name = "InGame"};
        }
        #endregion

        //--------------------//

        #region Engine Hook-in
        /// <inheritdoc />
        public override void HookIn()
        {
            base.HookIn();

            SwitchMusicTheme("Game", true);
        }

        /// <inheritdoc />
        public override void HookOut()
        {
            PrepareSave();

            base.HookOut();
        }
        #endregion

        #region Save
        /// <summary>
        /// Writes back data to <see cref="Universe"/> so that state gets stored in savegames.
        /// </summary>
        public void PrepareSave()
        {
            Universe.Camera = CameraState;
        }
        #endregion
    }
}
