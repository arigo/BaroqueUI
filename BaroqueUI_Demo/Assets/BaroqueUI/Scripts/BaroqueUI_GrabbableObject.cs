using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BaroqueUI
{
    public class BaroqueUI_GrabbableObject : MonoBehaviour
    {
        public string sceneActionName = "Default";

        void Start()
        {
            var delegates = SceneAction.Register(sceneActionName, this);
            delegates.buttonDown = OnButtonDown;
        }

        private bool OnButtonDown(ControllerSnapshot snapshot)
        {
            GrabNow.New(snapshot, transform);
            return true;   /* handled */
        }

        class GrabNow : AbstractControllerAction
        {
            Transform grabbed_object;
            Vector3 origin_position;
            Quaternion origin_rotation;

            public static void New(ControllerSnapshot snapshot, Transform grabbed_object)
            {
                GrabNow gn = snapshot.ThisControllerObject().AddComponent<GrabNow>();
                gn.grabbed_object = grabbed_object;
                gn.origin_rotation = Quaternion.Inverse(snapshot.rotation) * grabbed_object.rotation;
                gn.origin_position = Quaternion.Inverse(grabbed_object.rotation) * (grabbed_object.position - snapshot.position);
            }

            public override void HandleButtonMove(ControllerSnapshot snapshot)
            {
                grabbed_object.rotation = snapshot.rotation * origin_rotation;
                grabbed_object.position = snapshot.position + grabbed_object.rotation * origin_position;
            }

            public override bool HandleButtonUp()
            {
                Destroy(this);
                return true;
            }
        }
    }
}