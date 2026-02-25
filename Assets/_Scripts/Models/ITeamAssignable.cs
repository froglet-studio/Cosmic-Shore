using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CosmicShore.Models.Enums;

namespace CosmicShore.Models
{
    public interface ITeamAssignable
    {
        public void SetTeam(Domains domain);
    }
}
