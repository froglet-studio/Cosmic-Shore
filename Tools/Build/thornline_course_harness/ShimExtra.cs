// What the Thornline harness needs beyond race_course_source_harness's Unity math stub: Vector2 (the shipped
// NoseLeadSteering returns one) and Pose (ThornlineCourse.StartPoses).
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Pose(Vector3 position, Quaternion rotation) { this.position = position; this.rotation = rotation; }
    }
}
