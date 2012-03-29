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
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;
using Common;
using Common.Utils;
using Common.Values;
using Common.Values.Design;
using SlimDX;
using World.EntityComponents;

namespace World
{
    /// <summary>
    /// A <see cref="Positionable"/> on the <see cref="Terrain"/> whose behaviour and graphical representation is controlled by <see cref="World.EntityComponents"/>.
    /// </summary>
    public class Entity : Positionable
    {
        #region Events
        /// <summary>
        /// Occurs when <see cref="TemplateData"/> is about to change.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
        [Description("Occurs when TemplateData is about to change.")]
        public event Action<Entity> TemplateChanging;

        /// <summary>
        /// To be called when <see cref="TemplateData"/> is about to change.
        /// </summary>
        protected void OnTemplateChanging()
        {
            if (TemplateChanging != null) TemplateChanging(this);
        }

        /// <summary>
        /// Occurs when <see cref="TemplateData"/> has changed.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
        [Description("Occurs when TemplateData has changed.")]
        public event Action<Entity> TemplateChanged;

        /// <summary>
        /// To be called when <see cref="TemplateData"/> has changed.
        /// </summary>
        protected void OnTemplateChanged()
        {
            if (TemplateChanged != null) TemplateChanged(this);
        }
        #endregion

        #region Properties
        private string _className;

        /// <summary>
        /// The name of the <see cref="EntityTemplate"/>.
        /// </summary>
        /// <remarks>
        /// Setting this will overwrite <see cref="TemplateData"/> with a new clone of the appropriate <see cref="EntityTemplate"/>.
        /// This is serialized/stored in map files. It is also serialized/stored in savegames, but the value is ignored there (due to the attribute order)!
        /// </remarks>
        [XmlAttribute("Template"), Description("The name of the entity template")]
        public string TemplateName
        {
            get { return _className; }
            set
            {
                // Create copy of the class so run-time modifications for individual entities are possible
                TemplateData = TemplateManager.GetEntityTemplate(value).Clone();

                // Only set the new name once the according class was successfully located
                _className = value;
            }
        }

        private EntityTemplate _template;

        /// <summary>
        /// The <see cref="EntityTemplate"/> controlling the behavior and look for this <see cref="Entity"/>.
        /// </summary>
        /// <remarks>
        /// This is always a clone of the original <see cref="EntityTemplate"/>s.
        /// This is serialized/stored in savegames but not in map files!
        /// </remarks>
        [Browsable(false)]
        public EntityTemplate TemplateData
        {
            get { return _template; }
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

        /// <inheritdoc/>
        public override string ToString()
        {
            string value = base.ToString();
            if (!string.IsNullOrEmpty(_className))
                value += " (" + _className + ")";
            return value;
        }

        /// <summary>
        /// Controls how this <see cref="Entity"/> will move along a path generated by path-finding.
        /// </summary>
        [Browsable(false)]
        [XmlElement(typeof(PathLeader)), XmlElement(typeof(PathFollower))]
        public PathControl PathControl { get; set; }

        private float _rotation;

        /// <summary>
        /// The horizontal rotation of the view direction in degrees.
        /// </summary>
        [DefaultValue(0f), Description("The horizontal rotation of the view direction in degrees.")]
        [EditorAttribute(typeof(AngleEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public float Rotation { get { return _rotation; } set { UpdateHelper.Do(ref _rotation, value, OnRenderPropertyChanged); } }
        #endregion

        //--------------------//

        #region Collision
        /// <summary>
        /// Determines whether a certain point collides with this entity (based on its <see cref="CollisionControl"/> component).
        /// </summary>
        /// <param name="point">The point to check for collision in world space.</param>
        /// <returns><see langword="true"/> if the <paramref name="point"/> does collide with this entity, <see langword="false"/> otherwise.</returns>
        public bool CollisionTest(Vector2 point)
        {
            // With no valid collision control all collision checks always fail
            if (TemplateData.CollisionControl == null) return false;

            // Convert position from world space to entity space and transmit rotation
            var shiftedPoint = point - Position;
            return TemplateData.CollisionControl.CollisionTest(shiftedPoint, _rotation);
        }

        /// <summary>
        /// Determines whether a certain area collides with this entity (based on its <see cref="CollisionControl"/> component).
        /// </summary>
        /// <param name="area">The area to check for collision in world space.</param>
        /// <returns><see langword="true"/> if the <paramref name="area"/> does collide with this entity, <see langword="false"/> otherwise.</returns>
        public bool CollisionTest(Quadrangle area)
        {
            // With no valid collision control all collision checks always fail
            if (TemplateData.CollisionControl == null) return false;

            // Convert position from world space to entity space and transmit rotation
            var shiftedArea = area.Offset(-Position);
            return TemplateData.CollisionControl.CollisionTest(shiftedArea, _rotation);
        }
        #endregion

        #region Path finding
        /// <summary>
        /// Returns a list of positions that outline this <see cref="Entity"/>s <see cref="CollisionControl"/>.
        /// </summary>
        /// <returns>Positions in world space for use by the path finding system.</returns>
        internal Vector2[] GetPathFindingOutline()
        {
            // With no valid collision control the outline is empty
            if (TemplateData.CollisionControl == null) return new Vector2[] {};

            // Transmit rotation
            Vector2[] outline = TemplateData.CollisionControl.GetPathFindingOutline(_rotation);

            // Convert positions from entity space to world space
            for (int i = 0; i < outline.Length; i++)
                outline[i] += Position;

            return outline;
        }

        /// <summary>
        /// Perform movements queued up in <see cref="PathControl"/>.
        /// </summary>
        /// <param name="elapsedTime">How much game time in seconds has elapsed since this method was last called.</param>
        internal void UpdatePosition(double elapsedTime)
        {
            #region Sanity checks
            if (TemplateData.MovementControl == null) return;
            #endregion

            Vector2 posDifference;

            var leader = PathControl as PathLeader;
            if (leader != null && leader.PathNodes.Count > 0)
            { // This entity performs its own path finding

                #region Leader
                bool loop;
                do
                {
                    // Get the position of the next target node
                    Vector2 nextNodePos = leader.PathNodes.Peek();

                    // Calculate the difference between the current position and the target
                    posDifference = nextNodePos - Position;
                    float differenceLength = posDifference.Length();

                    // Calculate how much of the distance should be walked in this interval
                    var movementFactor = (float)(elapsedTime * TemplateData.MovementControl.Speed / differenceLength);

                    if (movementFactor >= 1)
                    { // This move will skip past the current node
                        // Remove the node from the list
                        leader.PathNodes.Pop();

                        // Subtract the amount of time the rest of the distance to the node would have taken
                        elapsedTime -= differenceLength / TemplateData.MovementControl.Speed;

                        if (leader.PathNodes.Count == 0)
                        { // No further nodes, go to final target
                            // Calculate the difference for the rotation calculation below
                            posDifference = leader.Target - Position;

                            // Move the entity
                            Position = leader.Target;

                            // Prevent further calls of this method
                            PathControl = null;

                            loop = false;
                        }
                        else
                        { // Continue with next node
                            loop = true;
                        }
                    }
                    else
                    { // We need to move a part of the way to the next node
                        // Move the entity
                        Position += posDifference * movementFactor;

                        loop = false;
                    }
                } while (loop);

                // Make the entity face the direction it is walking in
                Rotation = MathUtils.RadianToDegree((float)Math.Atan2(posDifference.Y, posDifference.X)) - 90;
                #endregion
            }
            else
            {
                var follower = PathControl as PathFollower;
                if (follower != null)
                { // This entity follows another entity for path finding

                    #region Follower
                    // ToDo: Implement
                    #endregion
                }
            }
        }
        #endregion

        //--------------------//

        #region Clone
        /// <summary>
        /// Creates a deep copy of this <see cref="Entity"/>.
        /// </summary>
        /// <returns>The cloned <see cref="Entity"/>.</returns>
        public virtual Entity CloneEntity()
        {
            var clonedEntity = (Entity)base.Clone();

            // Don't clone event handlers
            clonedEntity.TemplateChanged = null;
            clonedEntity.TemplateChanging = null;

            clonedEntity._template = _template.Clone();

            return clonedEntity;
        }

        /// <inheritdoc />
        public override Positionable Clone()
        {
            return CloneEntity();
        }
        #endregion
    }
}
