using UnityEngine;

namespace _Scripts._Core.Playfab_Models
{
    public class AuthenticationController : MonoBehaviour
    {
        // Start is called before the first frame update
        private void Start()
        {
            AuthenticationManager.Instance.AnonymousLogin();
        }

        public string RandomGenerateName()
        {
            AuthenticationManager.Instance.LoadRandomNameList();
            var adjectives = AuthenticationManager.Adjectives;
            var nouns = AuthenticationManager.Nouns;
            var random = new System.Random();
            var adj_index = random.Next(adjectives.Count);
            var noun_index = random.Next(nouns.Count);
            return $"{adjectives[adj_index]} {nouns[noun_index]}";
        }

        public void OnSetPlayerDisplayName()
        {
            
        }
    }
}
