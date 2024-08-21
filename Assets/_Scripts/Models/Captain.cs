using CosmicShore.Models.Enums;
using UnityEngine;
using UnityEngine.Serialization;
namespace CosmicShore.Models
{
    public class Captain
    {
        public SO_Captain SO_Captain { get; set; }

        // SO_Captain Fields (AKA, static properties)
        public string Name;
        public string Description;
        public string Flavor;
        public Sprite Image;
        public Sprite Icon;
        public Sprite SelectedIcon;
        public SO_Ship Ship;
        public Element PrimaryElement;
        [FormerlySerializedAs("Element")]
        public SO_Element SO_Element;
        public ResourceCollection InitialResourceLevels;

        // Dynamic Properties (change over the course of player playing)
        public int XP;
        int level;
        public int Level 
        {
            get { return level; }
            set
            {
                level = value;
                ResourceLevels = SO_Captain.InitialResourceLevels;
                switch (SO_Captain.PrimaryElement)
                {
                    case Element.Space:
                        ResourceLevels.Space = .1f * Level;
                        break;
                    case Element.Time:
                        ResourceLevels.Time = .1f * Level;
                        break;
                    case Element.Mass:
                        ResourceLevels.Mass = .1f * Level;
                        break;
                    case Element.Charge:
                        ResourceLevels.Charge = .1f * Level;
                        break;
                }
            }
        }
        
        public bool Unlocked;
        public bool Encountered;

        public ResourceCollection ResourceLevels;

        public Captain(SO_Captain so_Captain)
        {
            SO_Captain = so_Captain;

            Name = so_Captain.Name;
            Description = so_Captain.Description;
            Flavor = so_Captain.Flavor;
            Image = so_Captain.Image;
            Icon = so_Captain.Icon;
            SelectedIcon = so_Captain.SelectedIcon;
            Ship = so_Captain.Ship;
            PrimaryElement = so_Captain.PrimaryElement;
            SO_Element = so_Captain.Element;
            InitialResourceLevels = so_Captain.InitialResourceLevels;
        }
    }
}