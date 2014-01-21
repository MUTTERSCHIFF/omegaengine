/*
 * Copyright 2006-2014 Bastian Eicher
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this file,
 * You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;
using AlphaFramework.World.Paths;
using AlphaFramework.World.Templates;
using Common.Collections;

namespace AlphaFramework.World.Positionables
{
    /// <summary>
    /// A common base class for <see cref="Positionable{TCoordinates}"/> whose behaviour and graphical representation is controlled by components.
    /// </summary>
    /// <typeparam name="TSelf">The type of the class itself.</typeparam>
    /// <typeparam name="TCoordinates">Data type for storing position coordinates of objects in the game world.</typeparam>
    /// <typeparam name="TTemplate">The specific type of <see cref="EntityTemplateBase{TSelf}"/> to use as a component container.</typeparam>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "The first type parameter is a type self-reference and is always fixated by derived classes")]
    public abstract class EntityBase<TSelf, TCoordinates, TTemplate> : Positionable<TCoordinates>, ITemplateName, IUpdateable
        where TSelf : EntityBase<TSelf, TCoordinates, TTemplate>
        where TCoordinates : struct
        where TTemplate : EntityTemplateBase<TTemplate>
    {
        #region Events
        /// <summary>
        /// Occurs when <see cref="TemplateData"/> is about to change.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
        [Description("Occurs when TemplateData is about to change.")]
        public event Action<TSelf> TemplateChanging;

        /// <summary>
        /// To be called when <see cref="TemplateData"/> is about to change.
        /// </summary>
        protected void OnTemplateChanging()
        {
            if (TemplateChanging != null) TemplateChanging((TSelf)this);
        }

        /// <summary>
        /// Occurs when <see cref="TemplateData"/> has changed.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
        [Description("Occurs when TemplateData has changed.")]
        public event Action<TSelf> TemplateChanged;

        /// <summary>
        /// To be called when <see cref="TemplateData"/> has changed.
        /// </summary>
        protected void OnTemplateChanged()
        {
            if (TemplateChanged != null) TemplateChanged((TSelf)this);
        }
        #endregion

        #region Properties
        private string _templateName;

        /// <summary>
        /// The name of the <typeparamref name="TTemplate"/>.
        /// </summary>
        /// <remarks>
        /// Setting this will overwrite <see cref="TemplateData"/> with a new clone of the appropriate <typeparamref name="TTemplate"/>.
        /// This is serialized/stored in map files. It is also serialized/stored in savegames, but the value is ignored there (due to the attribute order)!
        /// </remarks>
        [XmlAttribute("Template"), Description("The name of the entity template")]
        public string TemplateName
        {
            get { return _templateName; }
            set
            {
                // Create copy of the class so run-time modifications for individual entities are possible
                TemplateData = Template<TTemplate>.All[value].Clone();

                // Only set the new name once the according class was successfully located
                _templateName = value;
            }
        }

        private TTemplate _template;

        /// <summary>
        /// The <typeparamref name="TTemplate"/> controlling the behavior and look for this <see cref="EntityBase{TSelf,TCoordinates,TTemplate}"/>.
        /// </summary>
        /// <remarks>
        /// This is always a clone of the original <typeparamref name="TTemplate"/>.
        /// This is serialized/stored in savegames but not in map files!
        /// </remarks>
        [Browsable(false)]
        public TTemplate TemplateData
        {
            get { return _templateDataMasked ? null : _template; }
            set
            {
                OnTemplateChanging();

                // Backup the original value (might be needed for restore on exception) and then set the new value
                var oldValue = _template;
                _template = value;

                try
                {
                    OnTemplateChanged();
                }
                    #region Error handling
                catch (Exception)
                {
                    // Restore the original value, trigger a render update for that and then pass on the exception for handling
                    _template = oldValue;
                    OnTemplateChanged();
                    throw;
                }
                #endregion
            }
        }

        /// <summary>
        /// Controls how this <see cref="EntityBase{TSelf,TCoordinates,TTemplate}"/> will move along a path generated by pathfinding.
        /// </summary>
        [Browsable(false)]
        [XmlIgnore] // XML serialization configuration is configured in sub-type
        public abstract PathControl<TCoordinates> PathControl { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            string value = base.ToString();
            if (!string.IsNullOrEmpty(_templateName))
                value += " (" + _templateName + ")";
            return value;
        }
        #endregion

        #region Masking
        // ReSharper disable once StaticFieldInGenericType
        private static bool _templateDataMasked;

        private struct TemplateDataMasking : IDisposable
        {
            public void Dispose()
            {
                _templateDataMasked = false;
            }
        }

        /// <summary>
        /// Makes all <see cref="TemplateData"/> values return <see langword="null"/> until <see cref="IDisposable.Dispose"/> is called on the returned object. This is not thread-safe!
        /// </summary>
        public static IDisposable MaskTemplateData()
        {
            _templateDataMasked = true;
            return new TemplateDataMasking();
        }
        #endregion

        //--------------------//

        #region Pathfinding
        /// <summary>
        /// Perform movements queued up in pathfinding.
        /// </summary>
        /// <param name="elapsedTime">How much game time in seconds has elapsed since this method was last called.</param>
        public virtual void Update(double elapsedTime)
        {
            if (PathControl != null)
            {
                new PerTypeDispatcher<PathControl<TCoordinates>>(ignoreMissing: false)
                {
                    (PathLeader<TCoordinates> leader) => UpdatePath(leader, elapsedTime),
                    (PathFollower<TCoordinates> follower) => UpdatePath(follower, elapsedTime),
                }.Dispatch(PathControl);
            }
        }

        /// <summary>
        /// Handles movement controlled by a <see cref="PathLeader{TCoordinates}"/>.
        /// </summary>
        protected abstract void UpdatePath(PathLeader<TCoordinates> leader, double elapsedTime);

        /// <summary>
        /// Handles movement controlled by a <see cref="PathFollower{TCoordinates}"/>.
        /// </summary>
        protected abstract void UpdatePath(PathFollower<TCoordinates> follower, double elapsedTime);
        #endregion

        //--------------------//

        #region Clone
        /// <summary>
        /// Creates a deep copy of this <see cref="EntityBase{TSelf,TCoordinates,TTemplate}"/>.
        /// </summary>
        /// <returns>The cloned <see cref="EntityBase{TSelf,TCoordinates,TTemplate}"/>.</returns>
        public virtual TSelf CloneEntity()
        {
            var clonedEntity = (TSelf)base.Clone();

            // Don't clone event handlers
            clonedEntity.TemplateChanged = null;
            clonedEntity.TemplateChanging = null;

            clonedEntity._template = _template.Clone();

            return clonedEntity;
        }

        /// <inheritdoc />
        public override Positionable<TCoordinates> Clone()
        {
            return CloneEntity();
        }
        #endregion
    }
}