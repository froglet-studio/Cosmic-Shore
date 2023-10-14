using Firebase.Auth;
using StarWriter.Utility.Singleton;

namespace _Scripts._Core.Firebase.Controller
{
    public class FirebaseAuthentication : SingletonPersistent<FirebaseAuthentication>
    {
        // User authentication
        private static FirebaseAuth _userAuthentication;
        
        // Developer authentication, recommended use: in UNITY_EDITOR directive
        private static FirebaseAuth _devAuthentication;
    }
}
