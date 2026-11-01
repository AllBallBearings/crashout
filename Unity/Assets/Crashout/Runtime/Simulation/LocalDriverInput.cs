using UnityEngine;
using UnityEngine.InputSystem;

namespace Crashout.Simulation
{
    public sealed class LocalDriverInput
    {
        private uint sequence;
        private float touchSteering;
        private bool touchThrottle;
        private bool touchBrake;
        private bool touchBoost;

        public void SetTouchBoost(bool boost)
        {
            touchBoost = boost;
        }

        public void SetTouchState(float steering, bool throttle, bool brake)
        {
            touchSteering = Mathf.Clamp(steering, -1f, 1f);
            touchThrottle = throttle;
            touchBrake = brake;
        }

        public DriverInputCommand Sample(uint tick, uint turnId)
        {
            Keyboard keyboard = Keyboard.current;
            float keyboardSteering = 0f;
            bool keyboardThrottle = false;
            bool keyboardBrake = false;
            bool keyboardBoost = false;

            if (keyboard != null)
            {
                keyboardSteering = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                keyboardThrottle = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                keyboardBrake = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                keyboardBoost = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }

            return new DriverInputCommand
            {
                ClientId = 0,
                TurnId = turnId,
                TargetTick = tick,
                Sequence = ++sequence,
                Steering = Mathf.Abs(touchSteering) > Mathf.Abs(keyboardSteering) ? touchSteering : keyboardSteering,
                Throttle = keyboardThrottle || touchThrottle ? 1f : 0f,
                Brake = keyboardBrake || touchBrake ? 1f : 0f,
                Reverse = keyboardBrake || touchBrake,
                Boost = keyboardBoost || touchBoost
            };
        }
    }
}
