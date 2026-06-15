using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeProject.Models
{
    public class Step : BindableBase
    {
        /// <summary>/// Prism Property/// </summary>
        private mouvment _mvt;

        public mouvment Mvt
        {
            get { return _mvt; }
            set { SetProperty(ref _mvt, value); }
        }
        /// <summary>/// Prism Property/// </summary>
		private int _id;

        public int Id
        {
            get { return _id; }
            set { SetProperty(ref _id, value); }
        }

        public Step(int id,mouvment mouvment = mouvment.None)
        {
            Id = id;
            Mvt = mouvment;
        }

    }
}
