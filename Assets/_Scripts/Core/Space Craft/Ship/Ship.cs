//using Mobius.Info;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ship : MonoBehaviour
{
    

    [Header("Assests")]
    [SerializeField]
    public GameObject shipPrefab;
    [SerializeField]
    public SO_Ship_Base startShipData;
    [SerializeField]
    public SO_Ship_Base currentShipData;

    private Ship ship;

    public Ship(SO_Ship_Base shipInfo)
    {
        shipName = shipInfo.ShipName;
        maxHealth = shipInfo.MaxHealth;
        maxEnergy = shipInfo.MaxEnergy;
    }

    
    public string shipName { get; set; }
    public float maxHealth{ get; set; }
    public float maxEnergy { get; set; }

    // Start is called before the first frame update
    void Start()
    {
        currentShipData = startShipData;
        Ship ship1 = new Ship(currentShipData);
        ship = ship1;
        Debug.Log(ship.maxEnergy);
        //shipPrefab = GetComponentInParent("Player");
        Instantiate<GameObject>(shipPrefab);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ResetShipsInfo()
    {
        Ship ship1 = new Ship(startShipData);
        ship = ship1;
    }

    /**
     * [Header("Components")]
        public NavMeshAgent agent;
        public Animator animator;

        [Header("Movement")]
        public float rotationSpeed = 100;

        [Header("Firing")]
        public KeyCode shootKey = KeyCode.Space;
        public GameObject projectilePrefab;
        public Transform projectileMount;

        void Update()
        {
            // movement for local player
            if (!isLocalPlayer)
                return;

            // rotate
            float horizontal = Input.GetAxis("Horizontal");
            transform.Rotate(0, horizontal * rotationSpeed * Time.deltaTime, 0);

            // move
            float vertical = Input.GetAxis("Vertical");
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            agent.velocity = forward * Mathf.Max(vertical, 0) * agent.speed;
            animator.SetBool("Moving", agent.velocity != Vector3.zero);

            // shoot
            if (Input.GetKeyDown(shootKey))
            {
                CmdFire();
            }
        }

        // this is called on the server
        [Command]
        void CmdFire()
        {
            GameObject projectile = Instantiate(projectilePrefab, projectileMount.position, transform.rotation);
            NetworkServer.Spawn(projectile);
            RpcOnFire();
        }

        // this is called on the tank that fired for all observers
        [ClientRpc]
        void RpcOnFire()
        {
            animator.SetTrigger("Shoot");
        }
    }
    **/
}
