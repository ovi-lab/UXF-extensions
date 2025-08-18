using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace UXF
{
    /// <summary>
    /// Attach this component to a gameobject and assign it in the trackedObjects field in an ExperimentSession to automatically record position/rotation of the object at each frame.
    /// Records the:
    /// - position (pos_x, pos_y, pos_z)
    /// - rotation as quaternion (rot_x, rot_y, rot_z, rot_w)
    /// - worldToLocalMatrix (wlm_00, wlm_01, wlm_02, wlm_03, wlm_10, wlm_11, wlm_12, wlm_13, wlm_20, wlm_21, wlm_22, wlm_23, wlm_30, wlm_31, wlm_32, wlm_33)
    /// </summary>
    /// <seealso cref="Transform.position"/>
    /// <seealso cref="Transform.rotation"/>
    /// <seealso cref="Transform.worldToLocalMatrix"/>
    /// <seealso cref="Matrix4x4"/>
    public class PositionRotationTracker : Tracker
    {
        public override string MeasurementDescriptor => "movement";
        public override IEnumerable<string> CustomHeader => new string[]
        {
            "pos_x", "pos_y", "pos_z", "rot_x", "rot_y", "rot_z", "rot_w",
            "wlm_00", "wlm_01", "wlm_02", "wlm_03", "wlm_10", "wlm_11", "wlm_12", "wlm_13",
            "wlm_20", "wlm_21", "wlm_22", "wlm_23", "wlm_30", "wlm_31", "wlm_32", "wlm_33",
        };

        /// <summary>
        /// Returns current position and rotation values
        /// </summary>
        /// <returns></returns>
        protected override UXFDataRow GetCurrentValues()
        {
            // get position and rotation
            Vector3 p = gameObject.transform.position;
            Quaternion r = gameObject.transform.rotation;
            Matrix4x4 m = gameObject.transform.worldToLocalMatrix;

            // return position, rotation (x, y, z) as an array
            var values = new UXFDataRow()
            {
                ("pos_x", p.x),
                ("pos_y", p.y),
                ("pos_z", p.z),
                ("rot_x", r.x),
                ("rot_y", r.y),
                ("rot_z", r.z),
                ("rot_w", r.w),
                ("wlm_00", m.m00),
                ("wlm_01", m.m01),
                ("wlm_02", m.m02),
                ("wlm_03", m.m03),
                ("wlm_10", m.m10),
                ("wlm_11", m.m11),
                ("wlm_12", m.m12),
                ("wlm_13", m.m13),
                ("wlm_20", m.m20),
                ("wlm_21", m.m21),
                ("wlm_22", m.m22),
                ("wlm_23", m.m23),
                ("wlm_30", m.m30),
                ("wlm_31", m.m31),
                ("wlm_32", m.m32),
                ("wlm_33", m.m33)
            };

            return values;
        }
    }
}
