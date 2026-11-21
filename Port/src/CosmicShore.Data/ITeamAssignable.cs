using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CosmicShore.Data;

namespace CosmicShore.Data
{
    public interface ITeamAssignable
    {
        public void SetTeam(Domains domain);
    }
}
