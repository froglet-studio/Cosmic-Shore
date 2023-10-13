using Firebase.Auth;
using StarWriter.Utility.Singleton;

namespace _Scripts._Core.Firebase.Controller
{
    public class FirebaseAuthentication : SingletonPersistent<FirebaseAuthentication>
    {
        private static FirebaseAuth _userAuthentication;
        private static FirebaseAuth _devAuthentication;
    }
}
