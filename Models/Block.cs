namespace StackTower.Models
{
    public class Block
    {
        public double X { get; set; }
        public double Y { get; set; } // For dropping physics
        public double Width { get; set; }
        public int Level { get; set; }
        public string Color { get; set; } = string.Empty;
        
        // Transient physics properties
        public double SpeedY { get; set; }
        public double RotationSpeed { get; set; }
        public double Rotation { get; set; } // For visual swing effect
        
        // For the current moving block
        public double Angle { get; set; } // Current swing angle

    }
}
