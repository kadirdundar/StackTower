using StackTower.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StackTower.Services
{
    public enum GameState
    {
        Ready,
        Swinging,
        Dropping,
        GameOver
    }

    public class GameEngine
    {
        public event Action? OnStateChanged;
        public event Action? OnBlockLanded;
        public event Action? OnGameOver;

        public List<Block> Stack { get; private set; } = new List<Block>();
        public List<Block> Debris { get; private set; } = new List<Block>();
        public Block CurrentBlock { get; private set; } = default!;
        public GameState State { get; private set; } = GameState.Ready;
        public int Score => Stack.Count - 1; // -1 because base block doesn't count
        
        // Settings
        private const double InitialWidth = 300;
        private const double BlockHeight = 40; 
        private const double GameWidth = 600;
        private double _swingSpeed = 0.8;
        private double _time = 0;
        
        // Pendulum
        private const double RopeLength = 200;
        private const double MaxSwingAngle = 1.0; // Radians (~57 degrees)
        private double _pivotX => GameWidth / 2;
        private double _pivotY => (Stack.Count * BlockHeight) + RopeLength + 100; // Pivot moves up with stack

        public GameEngine()
        {
            ResetGame();
        }

        public void ResetGame()
        {
            Stack.Clear();
            Debris.Clear();
            State = GameState.Ready;
            _swingSpeed = 0.8;
            _time = 0;
            
            // Base block
            Stack.Add(new Block
            {
                X = (GameWidth - InitialWidth) / 2,
                Y = 0,
                Width = InitialWidth,
                Level = 0,
                Color = "#7f8c8d" 
            });

       
        }

        private void SpawnNewBlock()
        {
            var prevBlock = Stack.Last();
            CurrentBlock = new Block
            {
                Width = prevBlock.Width, // Inherit width from previous
                Level = Stack.Count,
                Color = GetRandomColor(),
                Y = _pivotY - RopeLength // Start at roughly rope height (will be updated in Tick)
            };
            OnStateChanged?.Invoke();
        }

        public void Tick()
        {
            if (State == GameState.GameOver || State == GameState.Ready) return;

            if (State == GameState.Swinging)
            {
                _time += 0.05 * _swingSpeed;
                
                // Calculate Pendulum motion
                // Angle oscillates
                CurrentBlock.Angle = Math.Sin(_time) * MaxSwingAngle;
                
                // Position based on angle and pivot
                // x = pivot + L * sin(theta)
                // y = pivot - L * cos(theta) (CSS coordinate system: bottom is 0)
                // Actually in CSS bottom 0 is bottom.
                // PivotY is height from bottom.
                
                CurrentBlock.X = (_pivotX + RopeLength * Math.Sin(CurrentBlock.Angle)) - (CurrentBlock.Width / 2);
                CurrentBlock.Y = _pivotY - (RopeLength * Math.Cos(CurrentBlock.Angle)) - BlockHeight;
                
                // Visual Rotation (optional, maybe keep it flat for easier stacking?)
                // CurrentBlock.Rotation = CurrentBlock.Angle * (180 / Math.PI); 
                // Let's keep it flat for now as "ferris wheel seat" style to fix the stacking logic issues.
                // Or user explicitly said "ip üzerinde sallanıyor". Usually that means rotation.
                // If I rotate, I must reset rotation on drop.
                CurrentBlock.Rotation = CurrentBlock.Angle * (180.0 / Math.PI);
            }
            else if (State == GameState.Dropping)
            {
                // Gravity drop
                CurrentBlock.SpeedY += 1.5; // Gravity acceleration
                CurrentBlock.Y -= CurrentBlock.SpeedY;
                
                // While dropping, rotate back to 0?
                if (Math.Abs(CurrentBlock.Rotation) > 1)
                {
                    CurrentBlock.Rotation *= 0.6; // Smoothly correct to 0
                }
                else
                {
                    CurrentBlock.Rotation = 0;
                }

                // Check collision with top of stack
                double stackTop = Stack.Count * BlockHeight;
                if (CurrentBlock.Y <= stackTop)
                {
                    CurrentBlock.Y = stackTop;
                    CurrentBlock.Rotation = 0; // Force flat on land
                    LandBlock();
                }
            }
            
            // Update Debris
            for (int i = Debris.Count - 1; i >= 0; i--)
            {
                var debris = Debris[i];
                debris.SpeedY += 1.5;
                debris.Y -= debris.SpeedY;
                debris.Rotation += debris.RotationSpeed;
                
                if (debris.Y < -100)
                {
                    Debris.RemoveAt(i);
                }
            }

            OnStateChanged?.Invoke();
        }

        public void StartGame()
        {
            if (State == GameState.Ready)
            {
                SpawnNewBlock();
                State = GameState.Swinging;
                OnStateChanged?.Invoke();
            }
        }

        public void DropBlock()
        {
            if (State == GameState.Swinging)
            {
                State = GameState.Dropping;
            }
        }

        private void LandBlock()
        {
            var prevBlock = Stack.Last();
            
            // Calculate overlap
            // X is the Left edge.
            double currentStart = CurrentBlock.X;
            double currentEnd = CurrentBlock.X + CurrentBlock.Width;
            double prevStart = prevBlock.X;
            double prevEnd = prevBlock.X + prevBlock.Width;

            double overlapStart = Math.Max(currentStart, prevStart);
            double overlapEnd = Math.Min(currentEnd, prevEnd);
            double overlap = overlapEnd - overlapStart;

            if (overlap <= 0)
            {
                State = GameState.GameOver;
                OnGameOver?.Invoke();
            }
            else
            {
                // Trim
                CurrentBlock.X = overlapStart;
                CurrentBlock.Width = overlap;
                
                Stack.Add(CurrentBlock);
                
                // Create Debris
                double cutWidth = (currentEnd - currentStart) - overlap;
                if (cutWidth > 0)
                {
                    var debrisBlock = new Block
                    {
                        Y = CurrentBlock.Y,
                        Width = cutWidth,
                        Color = CurrentBlock.Color,
                        Level = CurrentBlock.Level,
                        SpeedY = -5, // Slight pop up
                        RotationSpeed = new Random().NextDouble() < 0.5 ? 5 : -5
                    };

                    if (currentStart < prevStart) // Cut from left
                    {
                        debrisBlock.X = currentStart;
                    }
                    else // Cut from right
                    {
                        debrisBlock.X = currentEnd - cutWidth; 
                        // Actually if currentEnd > prevEnd, the cut part is at the right end.
                        // currentEnd is 100. prevEnd is 80. Overlap is 80.
                        // Cut width 20.
                        // Debris X should be at 80.
                        debrisBlock.X = prevEnd;
                    }
                    
                    Debris.Add(debrisBlock);
                }
                
                // Increase difficulty
                _swingSpeed += 0.05;
                if (_swingSpeed > 2.0) _swingSpeed = 2.0;
                
                SpawnNewBlock();
                State = GameState.Swinging;
                OnBlockLanded?.Invoke();
            }
            OnStateChanged?.Invoke();
        }

        private string GetRandomColor()
        {
            string[] colors = { "#e74c3c", "#8e44ad", "#e67e22", "#2ecc71", "#f1c40f" };
            return colors[new Random().Next(colors.Length)];
        }
    }
}
