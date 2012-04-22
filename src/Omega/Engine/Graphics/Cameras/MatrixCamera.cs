/*
 * Copyright 2006-2012 Bastian Eicher
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this file,
 * You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.ComponentModel;
using Common;
using Common.Values;
using SlimDX;

namespace OmegaEngine.Graphics.Cameras
{
    /// <summary>
    /// A camera that internally uses matrixes for representing rotations.
    /// </summary>
    public abstract class MatrixCamera : Camera
    {
        #region Properties
        private DoubleVector3 _target;

        /// <summary>
        /// The position the camera is looking at.
        /// </summary>
        [Description("The position the camera is looking at."), Category("Layout")]
        public virtual DoubleVector3 Target { get { return _target; } set { UpdateHelper.Do(ref _target, value, ref ViewDirty, ref ViewFrustumDirty); } }

        private Vector3 _upVector = new Vector3(0, 1, 0);

        /// <summary>
        /// A vector indicating the up-direction
        /// </summary>
        [DefaultValue(typeof(Vector3), "0; 1; 0"), Description("A vector indicating the up-direction"), Category("Layout")]
        public Vector3 UpVector { get { return _upVector; } protected set { UpdateHelper.Do(ref _upVector, value, ref ViewDirty, ref ViewFrustumDirty); } }
        #endregion

        //--------------------//

        #region Recalc View Matrix
        /// <summary>
        /// Update cached versions of <see cref="View"/> and related matrices; abstract, to be overwritten in subclass.
        /// </summary>
        protected override void UpdateView()
        {
            SimpleViewCached = Matrix.LookAtLH(new Vector3(), _target.ApplyOffset(PositionCached), _upVector);
            ViewCached = Matrix.LookAtLH(PositionCached.ApplyOffset(PositionBaseCached), _target.ApplyOffset(PositionBaseCached), _upVector);

            CacheSpecialMatrices();
        }
        #endregion
    }
}