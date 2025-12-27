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
        public event Action? OnTinyBlockLanded;
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
        private double _swingSpeed = 2.0; // Reduced from 10.0 to match original feel (~1.5-2.0 rad/s)
        private double _time = 0;
        
        // Pendulum
        private double _ropeLength = 200;
        public double RopeLength 
        { 
            get => _ropeLength; 
            set 
            {
                _ropeLength = value;
                // Calculate max angle to keep block within screen (Radius ~ 250px)
                // Width 600. Center 300. Max acceptable offset ~250.
                // 250 = L * sin(theta) -> sin(theta) = 250 / L
                double ratio = 250.0 / Math.Max(_ropeLength, 1);
                if (ratio > 1.0) ratio = 1.0;
                MaxSwingAngle = Math.Asin(ratio);
            } 
        }
        private double MaxSwingAngle = 1.0; // Radians
        private double _pivotX => GameWidth / 2;
        public double PivotY => (Stack.Count * BlockHeight) + RopeLength + 100; // Pivot moves up with stack

        public GameEngine()
        {
            ResetGame();
        }

        public void ResetGame()
        {
            Stack.Clear();
            Debris.Clear();
            State = GameState.Ready;
            _swingSpeed = 1.5; // Start slower
            _time = 0;
            
            // Base block
            Stack.Add(new Block
            {
                X = (GameWidth - InitialWidth) / 2,
                Y = 0,
                Width = InitialWidth,
                Level = 0,
                Color = GetGradientColor(0) 
            });

       
        }

        private void SpawnNewBlock()
        {
            var prevBlock = Stack.Last();
            CurrentBlock = new Block
            {
                Width = prevBlock.Width, // Inherit width from previous
                Level = Stack.Count,
                Color = GetGradientColor(Stack.Count),
                Y = PivotY - RopeLength // Start at roughly rope height (will be updated in Tick)
            };
            OnStateChanged?.Invoke();
        }

        public void Tick(double deltaTime)
        {
            if (State == GameState.GameOver || State == GameState.Ready) return;

            if (State == GameState.Swinging)
            {
                // deltaTime is in seconds (e.g. 0.016 for 60fps)
                _time += deltaTime * _swingSpeed;
                
                // Calculate Pendulum motion
                CurrentBlock.Angle = Math.Sin(_time) * MaxSwingAngle;
                
                CurrentBlock.X = (_pivotX + RopeLength * Math.Sin(CurrentBlock.Angle)) - (CurrentBlock.Width / 2);
                CurrentBlock.Y = PivotY - (RopeLength * Math.Cos(CurrentBlock.Angle)) - BlockHeight;
                
                CurrentBlock.Rotation = CurrentBlock.Angle * (180.0 / Math.PI);
            }
            else if (State == GameState.Dropping)
            {
                // Gravity drop (acceleration * t^2 practically, but we do velocity iteration)
                // Original was 1.5 px/frame^2. 1.5 * 60 * 60 = 5400 px/s^2.
                const double Gravity = 5000.0; 
                CurrentBlock.SpeedY += Gravity * deltaTime; 
                CurrentBlock.Y -= CurrentBlock.SpeedY * deltaTime;
                
                // While dropping, rotate back to 0
                if (Math.Abs(CurrentBlock.Rotation) > 1)
                {
                    // Smoothly correct to 0 based on time
                    CurrentBlock.Rotation *= Math.Exp(-10.0 * deltaTime); // Faster correction
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
                    CurrentBlock.Rotation = 0; 
                    LandBlock();
                }
            }
            
            // Update Debris
            const double DebrisGravity = 5000.0;
            for (int i = Debris.Count - 1; i >= 0; i--)
            {
                var debris = Debris[i];
                debris.SpeedY += DebrisGravity * deltaTime;
                debris.Y -= debris.SpeedY * deltaTime;
                debris.Rotation += debris.RotationSpeed * deltaTime * 60; // Keep scale similar
                
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
                _swingSpeed += 0.1; // Small increment
                if (_swingSpeed > 5.0) _swingSpeed = 5.0;
                
                if (overlap <= prevBlock.Width * 0.5)
                {
                    OnTinyBlockLanded?.Invoke();
                }

                SpawnNewBlock();
                State = GameState.Swinging;
                OnBlockLanded?.Invoke();
            }
            OnStateChanged?.Invoke();
        }

        private string GetGradientColor(int level)
        {
            // Start red (0), cycle every 50 blocks
            int hue = (level * 10) % 360; 
            return $"hsl({hue}, 70%, 50%)";
        }
    }
}
