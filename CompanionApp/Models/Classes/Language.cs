using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompanionApp.Models.Classes
{
    public class Language
    {
        public string Name { get; set; }
        public string Image { get; set; }

        public Language(string name, string image)
        {
            this.Name = name;
            this.Image = image;
        }
    }
}
