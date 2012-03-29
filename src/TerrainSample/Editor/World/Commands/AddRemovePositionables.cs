﻿/*
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
using Common.Undo;
using World;

namespace AlphaEditor.World.Commands
{
    /// <summary>
    /// Adds/removes one or more <see cref="Positionable"/>ies to/from a <see cref="Universe"/>.
    /// </summary>
    internal abstract class AddRemovePositionables : SimpleCommand
    {
        #region Variables
        private readonly Universe _universe;

        // Note: Use List<> instead of Array, because the size of the incoming IEnumerable<> will be unkown
        private readonly List<Positionable> _positionables;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new command for adding/removing one or more <see cref="Positionable"/>ies to/from a <see cref="Universe"/>.
        /// </summary>
        /// <param name="universe">The <see cref="Universe"/> to add to / remove from.</param>
        /// <param name="positionables">The <see cref="Positionable"/>s to add/remove.</param>
        protected AddRemovePositionables(Universe universe, IEnumerable<Positionable> positionables)
        {
            #region Sanity checks
            if (universe == null) throw new ArgumentNullException("universe");
            if (positionables == null) throw new ArgumentNullException("positionables");
            #endregion

            _universe = universe;

            // Create local defensive copy of entities
            _positionables = new List<Positionable>(positionables);
        }
        #endregion

        //--------------------//

        #region Add/remove helpers
        /// <summary>
        /// Removes the entities from the universe
        /// </summary>
        protected void AddPositionables()
        {
            foreach (Positionable positionable in _positionables)
                _universe.Positionables.Add(positionable);
        }

        /// <summary>
        /// Adds the entities to the universe
        /// </summary>
        protected void RemovePositionables()
        {
            foreach (Positionable positionable in _positionables)
                _universe.Positionables.Remove(positionable);
        }
        #endregion
    }
}
