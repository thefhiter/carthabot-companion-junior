using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeProject.Models
{

    public enum mouvment
    {
        None,
        Forward,
        Backward,
        Left, 
        Right,
    }

    public enum Facing 
    {
        Up,
        Down,
        Left,
        Right,
    }
    public enum CellType
    {
        None,
        IsStart,
        IsFinish
    }
}
