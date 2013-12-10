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
using OmegaEngine;
using TerrainSample.Presentation;
using TerrainSample.World;

namespace TerrainSample.State
{
    /// <summary>
    /// Represents a state where the game's main menu is visible.
    /// </summary>
    public sealed class MenuState : GameState
    {
        #region Properties
        private readonly MenuPresenter _presenter;

        /// <inheritdoc/>
        public override Presenter Presenter { get { return _presenter; } }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new game state.
        /// </summary>
        /// <param name="engine">The engine to use for rendering.</param>
        /// <param name="universe">The universe to display as a map backgroud.</param>
        public MenuState(Engine engine, TerrainUniverse universe) : base(engine)
        {
            _presenter = new MenuPresenter(engine, universe);
        }
        #endregion

        //--------------------//

        #region Render
        /// <inheritdoc/>
        public override void Render(double elapsedTime)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
