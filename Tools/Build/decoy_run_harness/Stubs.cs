// The one Unity type DecoyRunCourse uses that the shared math stub (race_course_source_harness/UnityMath.cs) lacks.
namespace UnityEngine
{
    public struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Pose(Vector3 position, Quaternion rotation) { this.position = position; this.rotation = rotation; }
    }
}
