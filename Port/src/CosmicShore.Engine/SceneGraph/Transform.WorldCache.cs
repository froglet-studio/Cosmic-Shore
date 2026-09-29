namespace CosmicShore.Engine
{
    /// <summary>
    /// The cached world pose. World values compose through the parent chain, and reading one
    /// used to recompose the whole chain from scratch — every level re-reading its parent's
    /// position, rotation AND scale, which is quadratic in depth and costs quaternion and matrix
    /// work at every level. The cache keeps each transform's composed pose together with the
    /// local values and the parent stamp it was built from, and re-validates by comparing those
    /// (bitwise) instead of recomposing.
    ///
    /// Validation is by comparison rather than by dirty flags because <see cref="localRotation"/>
    /// is a public field (writes cannot be intercepted) and a RectTransform's local position is
    /// derived from anchor state. Composition is the same expression in the same order as the
    /// uncached form, so a cached read is bit-identical to what it replaces.
    /// </summary>
    public partial class Transform
    {
        static long s_worldStamp;

        bool _wValid;
        long _wStamp, _wParentStamp;
        Transform _wParentRef;
        Vector3 _cLocalPos, _cLocalScale;
        Quaternion _cSelfRot;

        Vector3 _wPos, _wLossy;
        Quaternion _wRot;

        long _mStamp = -1;
        Matrix4x4 _wMatrix;

        static bool Same(float a, float b)
            => System.BitConverter.SingleToInt32Bits(a) == System.BitConverter.SingleToInt32Bits(b);

        static bool Same(in Vector3 a, in Vector3 b) => Same(a.x, b.x) && Same(a.y, b.y) && Same(a.z, b.z);

        static bool Same(in Quaternion a, in Quaternion b)
            => Same(a.x, b.x) && Same(a.y, b.y) && Same(a.z, b.z) && Same(a.w, b.w);

        /// <summary>Brings the cached world pose up to date and returns its stamp.</summary>
        long EnsureWorld()
        {
            var wp = WorldParent;
            long pStamp = 0;
            if (wp is not null) pStamp = wp.EnsureWorld();

            var lp = localPosition;
            var lr = SelfRotation;
            var ls = localScale;

            if (_wValid && pStamp == _wParentStamp && ReferenceEquals(wp, _wParentRef)
                && Same(lp, _cLocalPos) && Same(lr, _cSelfRot) && Same(ls, _cLocalScale))
                return _wStamp;

            if (wp is null)
            {
                _wPos = lp;
                _wRot = lr;
                _wLossy = ls;
            }
            else
            {
                // Same expressions as the uncached getters: position = parent.TransformPoint(lp),
                // rotation = parent.rotation * localRotation, lossy = Scale(parent.lossy, ls).
                _wPos = wp._wPos + wp._wRot * Vector3.Scale(wp._wLossy, lp);
                _wRot = wp._wRot * lr;
                _wLossy = Vector3.Scale(wp._wLossy, ls);
            }

            _cLocalPos = lp;
            _cSelfRot = lr;
            _cLocalScale = ls;
            _wParentRef = wp;
            _wParentStamp = pStamp;
            _wStamp = ++s_worldStamp;
            _wValid = true;
            return _wStamp;
        }

        Vector3 WorldPosition { get { EnsureWorld(); return _wPos; } }
        Quaternion WorldRotation { get { EnsureWorld(); return _wRot; } }
        Vector3 WorldLossyScale { get { EnsureWorld(); return _wLossy; } }

        /// <summary>parent.localToWorld * TRS(local), cached against the world stamp.</summary>
        Matrix4x4 WorldMatrix
        {
            get
            {
                long stamp = EnsureWorld();
                if (_mStamp == stamp) return _wMatrix;
                var wp = _wParentRef;
                _wMatrix = wp is null
                    ? Matrix4x4.TRS(_cLocalPos, _cSelfRot, _cLocalScale)
                    : wp.WorldMatrix * Matrix4x4.TRS(_cLocalPos, localRotation, _cLocalScale);
                _mStamp = stamp;
                return _wMatrix;
            }
        }
    }
}
