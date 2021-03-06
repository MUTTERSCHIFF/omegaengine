﻿/*
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FrameOfReference.Presentation;
using FrameOfReference.World;
using FrameOfReference.World.Config;
using LuaInterface;
using NanoByte.Common;
using NanoByte.Common.Collections;
using NanoByte.Common.Storage;

namespace FrameOfReference
{
    partial class Game
    {
        #region Properties
        /// <summary>
        /// The current game session
        /// </summary>
        [LuaHide]
        public Session CurrentSession { get; private set; }

        /// <summary>
        /// The currently active presenter
        /// </summary>
        [LuaHide]
        public Presenter CurrentPresenter { get; private set; }
        #endregion

        //--------------------//

        #region Random Menu Map
        /// <returns>The name of the current menu background map</returns>
        private static string GetMenuMap()
        {
            return "Menu";
        }
        #endregion

        #region Preload Savegame
        /// <summary>
        /// Loads the auto-save into <see cref="CurrentSession"/> for later usage
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Any kind of problems loading the previous game session should be ignored")]
        private void PreloadPreviousSession()
        {
            try
            {
                LoadSavegame("Resume");
            }
            catch (FileNotFoundException)
            {
                Log.Info("No previous game session found");
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to restore previous game session: " + ex.Message);
                return;
            }
            Log.Info("Previous game session restored");
        }
        #endregion

        //--------------------//

        #region Load Menu
        /// <summary>
        /// Loads a map into <see cref="_menuUniverse"/> and switches the <see cref="CurrentState"/> to <see cref="GameState.Menu"/>
        /// </summary>
        /// <param name="name">The name of the map to load</param>
        public void LoadMenu(string name)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            #endregion

            _menuUniverse = name.EndsWith(
                // Does the name have file ending?
                Universe.FileExt, StringComparison.OrdinalIgnoreCase) ?
                // Real filename
                Universe.Load(Path.Combine(Locations.InstallBase, name)) :
                // Internal map name
                Universe.FromContent(name + Universe.FileExt);

            CleanupPresenter();
            if (_menuPresenter != null)
            { // Prevent the old presenter from being reused for fast switching
                _menuPresenter.Dispose();
                _menuPresenter = null;
            }
            InitializeMenuMode();
        }
        #endregion

        #region Load Map
        /// <summary>
        /// Loads a game map into <see cref="CurrentSession"/> and switches the <see cref="CurrentState"/> to <see cref="GameState.InGame"/>
        /// </summary>
        /// <param name="name">The name of the map to load</param>
        public void LoadMap(string name)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            #endregion

            CurrentSession = new Session(
                // Does the name have file ending?
                name.EndsWith(Universe.FileExt, StringComparison.OrdinalIgnoreCase) ?
                    // Real filename
                    Universe.Load(Path.Combine(Locations.InstallBase, name)) :
                    // Internal map name
                    Universe.FromContent(name + Universe.FileExt));

            CleanupPresenter();
            InitializeGameMode();

            // Note: Do not call before Presenter has been initialized
            CurrentSession.Lua = NewLua();
        }

        /// <summary>
        /// Loads a game map into <see cref="CurrentSession"/> and switches the <see cref="CurrentState"/> to <see cref="GameState.Modify"/>
        /// </summary>
        /// <param name="name">The name of the map to load</param>
        public void ModifyMap(string name)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            #endregion

            CurrentSession = new Session(
                // Does the name have file ending?
                name.EndsWith(Universe.FileExt, StringComparison.OrdinalIgnoreCase) ?
                    // Real filename
                    Universe.Load(Path.Combine(Locations.InstallBase, name)) :
                    // Internal map name
                    Universe.FromContent(name + Universe.FileExt));

            CleanupPresenter();
            InitializeModifyMode();

            // Note: Do not call before Presenter has been initialized
            CurrentSession.Lua = NewLua();
        }
        #endregion

        //--------------------//

        #region Save/Load Savegames
        /// <summary>
        /// Saves the <see cref="CurrentSession"/> as a savegame stored in the user's profile.
        /// </summary>
        /// <param name="name">The name of the savegame to write.</param>
        public void SaveSavegame(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            // If we are currently in-game, then the camera position must be explicitly stored/updated
            if (CurrentState == GameState.InGame || CurrentState == GameState.Pause)
                ((InGamePresenter)CurrentPresenter).PrepareSave();

            // Write to disk
            string path = Locations.GetSaveDataPath(GeneralSettings.AppName, true, name + Session.FileExt);
            CurrentSession.Save(path);
        }

        /// <summary>
        /// Loads a savegame from user's profile to replace the <see cref="CurrentSession"/>.
        /// </summary>
        /// <param name="name">The name of the savegame to load.</param>
        public void LoadSavegame(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            // Read from disk
            string path = Locations.GetSaveDataPath(GeneralSettings.AppName, true, name + Session.FileExt);
            CurrentSession = Session.Load(path);
            CurrentSession.Lua = NewLua();
        }

        /// <summary>
        /// Lists the names of all stored <see cref="Session"/>s.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "For LUA use"), SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "For LUA use")]
        public IEnumerable<string> GetSavegameNames()
        {
            var savegameDir = new DirectoryInfo(Locations.GetSaveDataPath(GeneralSettings.AppName, isFile: false));
            return savegameDir.GetFiles("*" + Session.FileExt)
                .Select(x => x.Name.Substring(0, x.Name.Length - Session.FileExt.Length))
                .Except("Resume");
        }
        #endregion
    }
}
