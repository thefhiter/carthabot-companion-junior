using CompanionApp.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompanionApp.Modules
{
    public class AtelierModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var region = containerProvider.Resolve<IRegionManager>();
            region.RegisterViewWithRegion("AtelierRegion", typeof(AtelierView));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
        }
    }
}
