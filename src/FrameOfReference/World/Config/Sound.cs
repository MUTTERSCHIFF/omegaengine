/*
 * Copyright 2006-2014 Bastian Eicher
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
using NanoByte.Common;

namespace FrameOfReference.World.Config
{
    /// <summary>
    /// Stores sound settings (turn music on or off, etc.).
    /// </summary>
    /// <seealso cref="Settings.Sound"/>
    public sealed class SoundSettings
    {
        #region Events
        /// <summary>
        /// Occurs when a setting in this group is changed.
        /// </summary>
        [Description("Occurs when a setting in this group is changed.")]
        public event Action Changed;

        private void OnChanged()
        {
            Changed?.Invoke();
            if (Settings.AutoSave && Settings.Current != null && Settings.Current.Sound == this) Settings.SaveCurrent();
        }
        #endregion

        #region Properties
        private bool _playMusic = true;

        /// <summary>
        /// Play in-game music
        /// </summary>
        [DefaultValue(true), Description("Play in-game music")]
        public bool PlayMusic { get { return _playMusic; } set { value.To(ref _playMusic, OnChanged); } }
        #endregion
    }
}
