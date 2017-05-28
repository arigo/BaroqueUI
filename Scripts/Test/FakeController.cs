#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Valve.VR;


namespace BaroqueUI
{
    /* The in "Window" menu of Unity, choose "Editor test runner". */

    public class FakeController : Controller
    {
        static Exception got_exception;

        static void Fail(Exception exc)
        {
            if (got_exception == null)
                got_exception = exc;    /* depending on where, it might be eaten... */
            throw exc;
        }
        static void Fail(string message)
        {
            Fail(new Exception(message));
        }

        public static void RunUnitTests()
        {
            got_exception = null;
            try
            {
                Setup();
                Run();
            }
            finally
            {
                Teardown();
            }
            if (got_exception != null)   /* argh, was eaten */
                throw got_exception;
            Debug.Log("All tests OK");
        }

        static void Setup()
        {
            Debug.Assert(Application.isEditor);
            Debug.Assert(!Application.isPlaying);
            Application.logMessageReceived += HandleLog;

            CleanControllers();
            left_ctrl = Baroque.GetSteamVRManager().left.AddComponent<FakeController>();
            right_ctrl = Baroque.GetSteamVRManager().right.AddComponent<FakeController>();
            Baroque._InitTests();
        }

        static void CleanControllers()
        {
            left_ctrl = right_ctrl = null;

            var golist = new GameObject[] { Baroque.GetSteamVRManager().left, Baroque.GetSteamVRManager().right };
            foreach (var go in golist)
            {
                Controller ctrl = go.GetComponent<FakeController>();
                if (ctrl != null)
                    DestroyImmediate(ctrl);
            }
        }

        static void Teardown()
        {
            CleanControllers();
            Application.logMessageReceived -= HandleLog;
        }

        static void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type != LogType.Log)
                Fail(type + ": " + logString + "\n" + stackTrace);
        }


        /*************************************************************************************************/

        static FakeController left_ctrl, right_ctrl;

        /* emulation data */
        bool t_tracking;
        Vector3 t_position;
        Quaternion t_rotation;
        Vector2 t_touchpad_position;
        bool t_trigger, t_grip, t_touchpad_touched, t_touchpad_pressed, t_menu;
        Collider[] t_overlapping;
        List<string> t_seen;
        static float t_time;

        static void ResetTracking()
        {
            left_ctrl.Reset();
            right_ctrl.Reset();
            t_time = 42;
        }

        void Reset()
        {
            t_tracking = true;
            t_position = Vector3.zero;
            t_rotation = Quaternion.identity;
            t_touchpad_position = Vector2.zero;
            t_trigger = false;
            t_grip = false;
            t_touchpad_touched = false;
            t_touchpad_pressed = false;
            t_menu = false;
            t_overlapping = null;
            t_seen = null;
        }

        protected override Vector3 ComputePosition()
        {
            return transform.position;   /* no small correction */
        }

        protected override bool GetControllerState(ref VRControllerState_t controllerState)
        {
            if (!t_tracking)
                return false;

            transform.position = t_position;
            transform.rotation = t_rotation;
            controllerState.rAxis0.x = t_touchpad_position.x;
            controllerState.rAxis0.y = t_touchpad_position.y;
            controllerState.ulButtonTouched = 0;
            controllerState.ulButtonPressed = 0;
            if (t_trigger) controllerState.ulButtonPressed |= 1UL << ((int)EVRButtonId.k_EButton_SteamVR_Trigger);
            if (t_grip) controllerState.ulButtonPressed |= 1UL << ((int)EVRButtonId.k_EButton_Grip);
            if (t_touchpad_touched) controllerState.ulButtonTouched |= 1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad);
            if (t_touchpad_pressed) controllerState.ulButtonPressed |= 1UL << ((int)EVRButtonId.k_EButton_SteamVR_Touchpad);
            if (t_menu) controllerState.ulButtonPressed |= 1UL << ((int)EVRButtonId.k_EButton_ApplicationMenu);

            return true;
        }

        protected override Collider[] GetOverlappingColliders(Vector3 current_position)
        {
            if (t_overlapping != null)
                return t_overlapping;
            return new Collider[0];
        }

        protected override float GetTime()
        {
            return t_time;
        }


        public static void ClearLogConsole()
        {
            /*  http://answers.unity3d.com/questions/578393/clear-console-through-code-in-development-build.html */
            Assembly assembly = Assembly.GetAssembly(typeof(SceneView));
            Type logEntries = assembly.GetType("UnityEditorInternal.LogEntries");
            MethodInfo clearConsoleMethod = logEntries.GetMethod("Clear");
            clearConsoleMethod.Invoke(new object(), null);
        }

        static void Seen(Controller ctrl, string msg)
        {
            (ctrl as FakeController).t_seen.Add(msg);
        }

        void SeeingStart()
        {
            t_seen = new List<string>();
        }

        static void AssertListEquals(string[] got, string[] expected)
        {
            int i = 0;
            while (true)
            {
                if (i == got.Length && i == expected.Length)
                    return;   /* equal */
            if (i == got.Length || i == expected.Length)
                    break;    /* one side is shorter than the other */
                if (got[i] != expected[i])
                    break;
                i++;
            }
            Debug.LogError("Got [" + string.Join(", ", got) + "], expected [" + string.Join(", ", expected) + "]");
        }

        void ShouldHaveSeen(params string[] msgs)
        {
            AssertListEquals(t_seen.ToArray(), msgs);
            t_seen = null;
        }

        void Check(MonoBehaviour hover, uint hover_lock, params string[] msgs)
        {
            SeeingStart();
            _UpdateAllControllers(GetControllers());
            Debug.Assert(hover == CurrentHoverTracker());
            Debug.Assert(hover_lock == tracker_hover_lock);
            Debug.Assert((active_touchpad_state == ActiveTouchpadState.None) == (active_touchpad == null));
            ShouldHaveSeen(msgs);
        }

        
        /*************************************************************************************************/


        class TouchpadTest : MonoBehaviour
        {
            public void TestStarts()
            {
                var ct = Controller.HoverTracker(this);
                ct.onEnter += (ctrl) => { Seen(ctrl, "OnEnter"); };
                ct.onMoveOver += (ctrl) => { Seen(ctrl, "OnMoveOver"); };
                ct.onLeave += (ctrl) => { Seen(ctrl, "OnLeave"); };
            }

            public void AddTouchPress()
            {
                var ct = Controller.HoverTracker(this);
                ct.onTouchPressDown += (ctrl) => { Seen(ctrl, "OnTouchPressDown@" + ctrl.position.x + "," + ctrl.position.y + "," + ctrl.position.z); };
                ct.onTouchPressDrag += (ctrl) => { Seen(ctrl, "OnTouchPressDrag@" + ctrl.position.x + "," + ctrl.position.y + "," + ctrl.position.z); };
                ct.onTouchPressUp += (ctrl) => { Seen(ctrl, "OnTouchPressUp@" + ctrl.position.x + "," + ctrl.position.y + "," + ctrl.position.z); };
            }

            public void AddTouchScroll()
            {
                var ct = Controller.HoverTracker(this);
                ct.onTouchScroll += (ctrl, position) => { Seen(ctrl, "OnTouchScroll(" + position.x + ", " + position.y + ")"); };
            }

            public void AddTouch()
            {
                var ct = Controller.HoverTracker(this);
                ct.onTouchDown += (ctrl) => { Seen(ctrl, "OnTouchDown@" + ctrl.position.x + "," + ctrl.position.y + "," + ctrl.position.z ); };
                ct.onTouchDrag += (ctrl) => { Seen(ctrl, "OnTouchDrag@" + ctrl.position.x + "," + ctrl.position.y + "," + ctrl.position.z); };
                ct.onTouchUp += (ctrl) => { Seen(ctrl, "OnTouchUp@" + ctrl.position.x + "," + ctrl.position.y + "," + ctrl.position.z); };
            }
        }

        static void Run()
        {
            ClearLogConsole();
            Debug.Log("=================== TEST RUN ===================");
            ResetTracking();

            _UpdateAllControllers(GetControllers());
            Debug.Assert(left_ctrl.is_tracking_active);
            Debug.Assert(!left_ctrl.touchpadTouched);
            Debug.Assert(!left_ctrl.touchpadPressed);

            var go = new GameObject("touchpad test");
            go.AddComponent<BoxCollider>().center = new Vector3(10, 0, 0);
            TouchpadTest tt = go.AddComponent<TouchpadTest>();
            tt.TestStarts();

            Debug.Log("Collider, not touching");
            left_ctrl.Check(null, 0 /*,nothing*/);

            Debug.Log("Collider, touching");
            left_ctrl.t_overlapping = new Collider[] { go.GetComponent<Collider>() };
            left_ctrl.Check(tt, 0, "OnEnter", "OnMoveOver");

            Debug.Log("Collider, staying");
            left_ctrl.Check(tt, 0, "OnMoveOver");

            Debug.Log("Collider, leaving");
            left_ctrl.t_overlapping = null;
            left_ctrl.Check(null, 0, "OnLeave");

            const uint TLOCK = 1U << (int)EControllerButton.Touchpad;

            for (int action1 = 0; action1 < 2; action1++)
                for (int action2 = 0; action2 < 2; action2++)
                    for (int action3 = 0; action3 < 2; action3++)
                    {
                        bool act1 = action1 != 0;
                        bool act2 = action2 != 0;
                        bool act3 = action3 != 0;
                        Debug.Log("Touchpad, " + act1 + "/" + act2 + "/" + act3);
                        DestroyImmediate(tt);
                        tt = go.AddComponent<TouchpadTest>();
                        tt.TestStarts();
                        if (act1) tt.AddTouchPress();
                        if (act2) tt.AddTouchScroll();
                        if (act3) tt.AddTouch();

                        left_ctrl.t_overlapping = null;
                        left_ctrl.Check(null, 0 /*,nothing, we're outside*/);
                        left_ctrl.t_overlapping = new Collider[] { go.GetComponent<Collider>() };
                        left_ctrl.Check(tt, 0, "OnEnter", "OnMoveOver");
                        left_ctrl.Check(tt, 0, "OnMoveOver");

                        if (!act1 && !act2 && act3)
                        {
                            /* if we have act3 only, detect the touch immediately and ignore presses or finger moves */
                            left_ctrl.t_touchpad_touched = true;
                            left_ctrl.t_position = new Vector3(1, 2, 3);
                            left_ctrl.Check(tt, TLOCK, "OnTouchDown@1,2,3", "OnTouchDrag@1,2,3");
                            left_ctrl.t_position = new Vector3(1, 2, 4);
                            left_ctrl.Check(tt, TLOCK, "OnTouchDrag@1,2,4");
                            left_ctrl.t_touchpad_position = new Vector2(1, 6);   /* ignored */
                            left_ctrl.Check(tt, TLOCK, "OnTouchDrag@1,2,4");
                            left_ctrl.t_touchpad_position = new Vector2(0, 0);
                            left_ctrl.Check(tt, TLOCK, "OnTouchDrag@1,2,4");
                            left_ctrl.t_touchpad_pressed = true;   /* ignored */
                            left_ctrl.t_position = new Vector3(1, 2, 5);
                            left_ctrl.Check(tt, TLOCK, "OnTouchDrag@1,2,5");
                            left_ctrl.Check(tt, TLOCK, "OnTouchDrag@1,2,5");
                            left_ctrl.t_touchpad_pressed = false;
                            left_ctrl.Check(tt, TLOCK, "OnTouchDrag@1,2,5");
                            left_ctrl.Check(tt, TLOCK, "OnTouchDrag@1,2,5");
                            left_ctrl.t_touchpad_touched = false;
                            left_ctrl.t_position = new Vector3(1, 2, 6);
                            left_ctrl.Check(tt, 0, "OnTouchUp@1,2,5", "OnMoveOver");
                            Debug.Assert(left_ctrl.position == new Vector3(1, 2, 6));
                        }
                        else if (act1 || act2 || act3)
                        {
                            left_ctrl.t_position = new Vector3(1.001f, 0, 0);
                            left_ctrl.t_touchpad_touched = true;
                            left_ctrl.Check(tt, TLOCK, "OnMoveOver");
                            if (!act2) left_ctrl.t_touchpad_position = new Vector2(-1, -2);  /* ignored */
                            left_ctrl.t_position = new Vector3(1.002f, 0, 0);
                            left_ctrl.Check(tt, TLOCK, "OnMoveOver");
                            left_ctrl.t_position = new Vector3(1, 0, 0);
                            left_ctrl.t_touchpad_touched = false;
                            if (act3)
                                left_ctrl.Check(tt, 0, "OnTouchDown@1.001,0,0", "OnTouchDrag@1.001,0,0", "OnTouchUp@1.001,0,0", "OnMoveOver");
                            else
                                left_ctrl.Check(tt, 0, "OnMoveOver");
                            left_ctrl.Check(tt, 0, "OnMoveOver");

                            for (int i = 0; i < 2; i++)
                            {
                                left_ctrl.t_position = new Vector3(0, 2.001f, 0);
                                left_ctrl.t_touchpad_touched = true;
                                left_ctrl.Check(tt, TLOCK, "OnMoveOver");
                                if (!act2) left_ctrl.t_touchpad_position += new Vector2(-1, -2);  /* ignored */
                                left_ctrl.Check(tt, TLOCK, "OnMoveOver");
                                Vector3 old_pos = left_ctrl.t_position;
                                switch (i)
                                {
                                    case 0: t_time += 0.5f; break;                                 /* timeout elapsed */
                                    case 1: left_ctrl.t_position += new Vector3(0, 1, 0); break;   /* move controller */
                                }
                                Vector3 new_pos = left_ctrl.t_position;
                                if (act3)
                                {
                                    left_ctrl.Check(tt, TLOCK, "OnTouchDown@" + old_pos.x + "," + old_pos.y + "," + old_pos.z,
                                                               "OnTouchDrag@" + new_pos.x + "," + new_pos.y + "," + new_pos.z);
                                    left_ctrl.Check(tt, TLOCK, "OnTouchDrag@" + new_pos.x + "," + new_pos.y + "," + new_pos.z);
                                    left_ctrl.t_touchpad_touched = false;
                                    left_ctrl.Check(tt, 0, "OnTouchUp@" + new_pos.x + "," + new_pos.y + "," + new_pos.z, "OnMoveOver");
                                }
                                else
                                {
                                    left_ctrl.Check(tt, TLOCK, "OnMoveOver");
                                    left_ctrl.t_touchpad_touched = false;
                                }
                                left_ctrl.Check(tt, 0, "OnMoveOver");
                            }

                            if (act2)
                            {
                                left_ctrl.t_touchpad_position = new Vector2(0, 0);
                                left_ctrl.t_touchpad_touched = true;
                                left_ctrl.Check(tt, TLOCK, "OnMoveOver");
                                left_ctrl.t_touchpad_position = new Vector2(1, 2);
                                left_ctrl.Check(tt, TLOCK, "OnTouchScroll(1, 2)");
                                left_ctrl.t_position += new Vector3(2, 0, 0);         /* ignored */
                                left_ctrl.Check(tt, TLOCK, "OnTouchScroll(0, 0)");
                                left_ctrl.t_touchpad_position = new Vector2(1, 6);
                                left_ctrl.Check(tt, TLOCK, "OnTouchScroll(0, 4)");
                                left_ctrl.Check(tt, TLOCK, "OnTouchScroll(0, 0)");
                                for (int i = 0; i < 5; i++)
                                {
                                    left_ctrl.t_position = new Vector3(3, 2, 1);
                                    left_ctrl.t_touchpad_pressed = true;
                                    if (act1)
                                    {
                                        left_ctrl.Check(tt, TLOCK, "OnTouchPressDown@3,2,1", "OnTouchPressDrag@3,2,1");
                                        left_ctrl.t_position = new Vector3(3, 2, 2);
                                        left_ctrl.Check(tt, TLOCK, "OnTouchPressDrag@3,2,2");
                                        left_ctrl.t_touchpad_pressed = false;
                                        left_ctrl.t_position = new Vector3(3, 2, 3);
                                        left_ctrl.Check(tt, 0, "OnTouchPressUp@3,2,2", "OnMoveOver");
                                        left_ctrl.Check(tt, 0, "OnMoveOver");
                                        left_ctrl.t_position += new Vector3(2, 0, 0);         /* ignored */
                                        left_ctrl.Check(tt, 0, "OnMoveOver");
                                        if (i != 2)
                                        {
                                            left_ctrl.t_touchpad_position += new Vector2(0, 1);
                                            left_ctrl.Check(tt, TLOCK, "OnTouchScroll(0, 1)");
                                            left_ctrl.Check(tt, TLOCK, "OnTouchScroll(0, 0)");
                                        }
                                    }
                                    else
                                    {
                                        left_ctrl.Check(tt, TLOCK, "OnTouchScroll(0, 0)");
                                        left_ctrl.t_touchpad_pressed = false;
                                        left_ctrl.Check(tt, TLOCK, "OnTouchScroll(0, 0)");
                                    }
                                }
                                left_ctrl.t_touchpad_touched = false;
                                left_ctrl.Check(tt, 0, "OnMoveOver");
                                left_ctrl.t_position += new Vector3(2, 0, 0);         /* ignored */
                                left_ctrl.Check(tt, 0, "OnMoveOver");
                            }

                            if (act1)
                            {
                                left_ctrl.t_touchpad_touched = true;
                                for (int i = 0; i < 5; i++)
                                {
                                    left_ctrl.t_touchpad_pressed = true;
                                    left_ctrl.t_position = new Vector3(3, 1, 2);
                                    left_ctrl.Check(tt, TLOCK, "OnTouchPressDown@3,1,2", "OnTouchPressDrag@3,1,2");
                                    left_ctrl.t_position = new Vector3(3, 1, 4);
                                    left_ctrl.Check(tt, TLOCK, "OnTouchPressDrag@3,1,4");
                                    left_ctrl.t_touchpad_pressed = false;
                                    left_ctrl.t_position = new Vector3(3, 1, 5);
                                    left_ctrl.Check(tt, 0, "OnTouchPressUp@3,1,4", "OnMoveOver");
                                    left_ctrl.Check(tt, 0, "OnMoveOver");
                                }
                                left_ctrl.t_touchpad_touched = false;
                                left_ctrl.Check(tt, 0, "OnMoveOver");
                            }
                            else if (act3)
                            {
                                left_ctrl.t_touchpad_touched = true;
                                left_ctrl.t_position = new Vector3(3, 1, 1);
                                left_ctrl.Check(tt, TLOCK, "OnMoveOver");
                                left_ctrl.t_touchpad_pressed = true;          /* not handled, but still forces OnTouchDown immediately */
                                left_ctrl.t_position = new Vector3(3, 1, 2);
                                left_ctrl.Check(tt, TLOCK, "OnTouchDown@3,1,1", "OnTouchDrag@3,1,2");
                                left_ctrl.t_position = new Vector3(3, 1, 4);
                                left_ctrl.Check(tt, TLOCK, "OnTouchDrag@3,1,4");
                                left_ctrl.t_touchpad_pressed = false;
                                left_ctrl.t_position = new Vector3(3, 1, 5);
                                left_ctrl.Check(tt, TLOCK, "OnTouchDrag@3,1,5");
                                left_ctrl.t_touchpad_touched = false;
                                left_ctrl.t_position = new Vector3(3, 1, 6);
                                left_ctrl.Check(tt, 0, "OnTouchUp@3,1,5", "OnMoveOver");
                            }
                        }
                        else
                        {
                            /* if we have nothing, ignore the touchpad completely */
                            left_ctrl.t_touchpad_touched = true;
                            left_ctrl.Check(tt, 0, "OnMoveOver");
                            left_ctrl.t_touchpad_position = new Vector2(-2, -3);
                            left_ctrl.Check(tt, 0, "OnMoveOver");
                            left_ctrl.t_touchpad_pressed = true;
                            left_ctrl.Check(tt, 0, "OnMoveOver");
                            left_ctrl.t_touchpad_pressed = false;
                            left_ctrl.Check(tt, 0, "OnMoveOver");
                            left_ctrl.t_touchpad_touched = false;
                        }
                        left_ctrl.Check(tt, 0, "OnMoveOver");
                    }
        }
    }
}
#endif