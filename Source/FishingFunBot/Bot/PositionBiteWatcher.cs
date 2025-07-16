using log4net;
using System;
using System.Collections.Generic;
using System.Drawing;

#nullable enable
namespace FishingFun
{
    /// <summary>
    /// A bite detection system that monitors the vertical position of a fishing bobber to detect fish bites.
    /// 
    /// The class implements a position-based bite detection algorithm that works by:
    /// 1. Tracking the historical Y-coordinates of the bobber position over time
    /// 2. Calculating the median position from the historical data to establish a baseline
    /// 3. Comparing the current bobber position against this baseline
    /// 4. Detecting significant downward movement (beyond a threshold) as a fish bite
    /// 5. Raising appropriate fishing events throughout the process
    /// 
    /// The algorithm is designed to be robust against minor bobber movements and water effects
    /// by using the median position as a stable reference point rather than relying on absolute positions.
    /// </summary>
    public class PositionBiteWatcher : IBiteWatcher
    {
        /// <summary>
        /// Logger instance for debugging and monitoring the fishing bot's bite detection activity
        /// </summary>
        private static ILog logger = LogManager.GetLogger("Fishbot");

        /// <summary>
        /// Collection of historical Y-coordinates of the bobber position, maintained in sorted order.
        /// This list is used to calculate the median position which serves as the baseline for bite detection.
        /// </summary>
        private List<int> yPositions = new List<int>();
        
        /// <summary>
        /// The threshold value (in pixels) that determines when a bite is detected.
        /// A bite is registered when the bobber moves downward from the median position by this amount or more.
        /// </summary>
        private int strikeValue;
        
        /// <summary>
        /// The current difference between the median Y position and the current bobber Y position.
        /// A negative value indicates the bobber is below the median (potential bite condition).
        /// </summary>
        private int yDiff;
        
        /// <summary>
        /// Timer that periodically raises BobberMove events to notify observers of bobber position changes.
        /// Configured to fire every 500ms with a maximum duration of 25 seconds.
        /// </summary>
        private TimedAction? timer;

        /// <summary>
        /// Event handler that is invoked when fishing events occur (Reset, BobberMove, Loot).
        /// This allows external systems to respond to different phases of the fishing process.
        /// </summary>
        public Action<FishingEvent> FishingEventHandler { set; get; } = (e)=> { };

        /// <summary>
        /// Initializes a new instance of the PositionBiteWatcher with the specified strike threshold.
        /// </summary>
        /// <param name="strikeValue">The threshold value (in pixels) for detecting a bite. 
        /// When the bobber moves downward from the median position by this amount or more, a bite is detected.</param>
        public PositionBiteWatcher(int strikeValue)
        {
            this.strikeValue = strikeValue;
        }

        /// <summary>
        /// Raises a fishing event by invoking the registered event handler.
        /// This method serves as a centralized point for all fishing event notifications.
        /// </summary>
        /// <param name="ev">The fishing event to be raised, containing action type and optional amplitude data</param>
        public void RaiseEvent(FishingEvent ev)
        {
            FishingEventHandler?.Invoke(ev);
        }

        /// <summary>
        /// Resets the bite detection system for a new fishing attempt.
        /// This method initializes the tracking system with the initial bobber position and starts the movement timer.
        /// </summary>
        /// <param name="InitialBobberPosition">The starting position of the bobber when the fishing line is cast</param>
        public void Reset(Point InitialBobberPosition)
        {
            // Notify observers that the bite detection system is being reset
            RaiseEvent(new FishingEvent { Action = FishingAction.Reset });

            // Initialize the position tracking with the starting bobber position
            yPositions = new List<int>();
            yPositions.Add(InitialBobberPosition.Y);
            
            // Start the periodic timer that will fire BobberMove events every 500ms for up to 25 seconds
            // The timer reports the current amplitude (yDiff) to observers
            timer = new TimedAction((a) =>
            {
                RaiseEvent(new FishingEvent { Amplitude = yDiff, Action = FishingAction.BobberMove });
            }, 500, 25);
        }

        /// <summary>
        /// Analyzes the current bobber position to determine if a fish bite has occurred.
        /// This is the core method of the bite detection algorithm that gets called repeatedly during fishing.
        /// </summary>
        /// <param name="currentBobberPosition">The current position of the bobber on screen</param>
        /// <returns>True if a bite is detected (bobber moved down significantly), false otherwise</returns>
        public bool IsBite(Point currentBobberPosition)
        {
            // Add the current Y position to our historical data if it's not already recorded
            // This builds up a history of where the bobber has been positioned
            if (!yPositions.Contains(currentBobberPosition.Y))
            {
                yPositions.Add(currentBobberPosition.Y);
                yPositions.Sort(); // Keep positions sorted for efficient median calculation
            }

            // Calculate the median Y position from historical data
            // The formula (count + 0.5) / 2 gives us the middle index for median calculation
            // This provides a stable baseline that's resistant to outliers and temporary movements
            yDiff = yPositions[(int)((((double)yPositions.Count) + 0.5) / 2)] - currentBobberPosition.Y;

            // Check if the bobber has moved downward beyond our strike threshold
            // A negative yDiff means the current position is below the median (bobber sank)
            // The strikeValue defines how far down constitutes a bite
            bool thresholdReached = yDiff <= -strikeValue;

            // Execute the periodic timer if it's active
            // This ensures BobberMove events are raised at regular intervals
            if (timer != null)
            {
                timer.ExecuteIfDue();
            }

            // If bite threshold is reached, handle the bite detection
            if (thresholdReached)
            {
                // Raise the Loot event to signal that a fish has been caught
                RaiseEvent(new FishingEvent { Action = FishingAction.Loot });
                
                // Execute the timer immediately to send a final BobberMove event
                if (timer != null)
                {
                    timer.ExecuteNow();
                }
                return true; // Bite detected
            }

            return false; // No bite detected
        }
    }
}