BaroqueUI
=========

A Unity package giving a "standard" user interface for HTC Vive
applications.

This can be used for games, but is also meant to give standardization
more typical of non-game desktop applications.  Instead of giving a lot
of options, it tries to promote one particular use of the controllers
and the UI elements.  The hope is that it should give a better basis for
VR applications that are not meant to be games.


Status
------

Preliminary.  For Unity 5.5 (other versions not tested but may work).
Developed for HTC Vive.


Demo
----

This repo is meant to be checked out inside an ``Assets/`` directory of a Unity project.  This project should have SteamVR installed too, e.g. from the Assets Store.

For example scene(s) see the ``Examples/`` folder.



Programming overview
--------------------

All classes are in the BaroqueUI package (which you can access with
``using BaroqueUI;``).  Some classes are components that install
themselves automatically at runtime; there is no need to add them in the
editor.

In a few steps:

* You drag and drop the ``Teleport Enabler`` prefab in the scene (from
  (``BaroqueUI/Prefabs``) if you want the default behavior of enabling
  teleporting to move around.

* For each "interactable" object, you make sure it has got a collider
  and then attach a script to it.

* In the typical case, the script should inherit from ``ControllerTracker``
  instead of ``MonoBehaviour``.

* The BaroqueUI framework will call the ``OnXxx`` methods when the
  controller interacts with the object.  See the
  ``BaseControllerTracker`` and ``ControllerTracker`` for more details.

* The BaroqueUI framework also provides classes to display a keyboard
  and interact with dialog-box-like displays.

(...write more here...)



Reference
---------


BaroqueUI
+++++++++

This class contains only static methods.  The most important ones:

* ``BaroqueUI.BaroqueUI.GetHeadTransform()`` returns the Transform of
  the headset.

* ``BaroqueUI.BaroqueUI.GetControllers()`` returns an array of
  ``Controller`` objects.  See below.


Controller
++++++++++

``Controller`` is a component that installs itself on the ``Controller
(left)`` and ``Controller (right)`` objects in ``SteamVR``.  Public
interface:

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;
        public Vector3 velocity;
        public Vector3 angularVelocity;

Returns the location of the controller.  This is the position and
velocity of a point that is slightly in front of the controller, which
plays the role of "the position of the mouse pointer" in BaroqueUI.

        public bool triggerPressed;    // below the controller
        public bool touchpadPressed;   // big round area
        public bool gripPressed;       // side buttons
        public bool menuPressed;       // top small button
        public bool GetButton(EControllerButton btn);

Check whether individual buttons are pressed or not.

        public bool touchpadTouched;
        public Vector2 touchpadPosition;

Check whether the finger is touching the touchpad and where.
The position contains X and Y coordinates between -1 and 1.

        public BaseControllerTracker HoverControllerTracker();
        public BaseControllerTracker GrabbedControllerTracker()

Return which ``BaseControllerTracker`` (see below) the controller
is currently over, or is currently locked inside.

        public void GrabFromScript(bool active);

Lock or unlock the current hover area manually.  It is also locked
automatically by pressing the trigger or the grip button.

        public void HapticPulse(int durationMicroSec = 500);

Send a haptic pulse to the controller.

        public GameObject SetPointer(string pointer_name);
        public GameObject SetPointerPrefab(GameObject prefab);

Set the "mouse pointer".  This is a small object that is visible in
front of the controller, centered at the point that is at
``Controller.position``.  If given by name, it must be a GameObject from
a ``Resources`` directory, in a subdirectory ``Pointers``.  When either
function is called with ``null``, the pointer is removed.

        public void SetScrollWheel(bool visible);

Show or hide the scroll wheel on the trackpad.

        public void SetControllerHints(string trigger = null, string grip = null,
            string touchpadTouched = null, string touchpadPressed = null, string menu = null);

Set or unset controller hints.  The hints are attached to particular
buttons and should be small strings describing what the button does.
The hints are only shown if the controller is not moving much, like
typical for 2D mouse pointer hints.

        public int index;

The index of this controller: 0 for left, or 1 for right.  This is the
index inside the ``BaroqueUI.BaroqueUI.GetControllers()`` array.

        public T GetAdditionalData<T>(ref T[] locals) where T: new();

Attach additional local, controller-specific data.  To use this, you
pass by reference an array of some type ``T``, which should be a small
local class.  The array does not need to be initialized; this method
does it for you.  It returns the ``index``th item of the array,
instantiating it if it is still null.


BaseControllerTracker
+++++++++++++++++++++

For every object in the scene that you want to interact with, you need
to make a script and change its parent from ``MonoBehaviour`` to one of
``ControllerTracker`` or ``ConcurrentControllerTracker``.  You also need
to make sure the GameObject or its children contain at least one
collider, typically of the "trigger" kind.  This is used to know the
maximal interaction area.

Then you can override the following methods (the default implementation
does nothing):

        public virtual void OnEnter(Controller controller);
        public virtual void OnLeave(Controller controller);

Called when the controller enters or leaves the interaction area.
``OnEnter`` is always called before any other ``OnXxx``, and ``OnLeave``
is always called last.  Moreover, a given controller cannot be in two
different GameObject's areas at once: we always call ``OnLeave`` on the
first area before calling ``OnEnter`` on the second one.

        public virtual void OnTriggerDown(Controller controller);
        public virtual void OnTriggerUp(Controller controller);
        public virtual void OnGripDown(Controller controller);
        public virtual void OnGripUp(Controller controller);

Called when the trigger or the grip button are pressed or released
inside the area.  While one of them is pressed, the interaction area is
locked, i.e. we won't receive any ``OnLeave``.  Note that if the user
presses both buttons at once, only the first one causes these methods
from being called.

        public virtual float GetPriority(Controller controller);

Get the "priority" of this interaction area.  Higher priority areas
correspond to higher numbers.  If you return ``float.NegativeInfinity``
then the controller is considered not to be in the area at all, even
though it touches the colliders; this is useful to have finer-grained
detection.  If several GameObjects return a different value, the one
with the highest priority wins.  Right now, the default is minus the
size of the bounding box of the largest collider.  This default does not
depend on the position of the controller; in some cases you want it to
return a higher number if the controller is closer to some specific
"center" point, to help distinguish between two close GameObjects.

        public virtual bool CanStartTeleportAction(Controller controller);

If you override this method and return false, then the Teleport Enabler
action will not start even if the user pressed the touchpad.  Useful if
this area has another usage for the touchpad.


ControllerTracker
+++++++++++++++++

The most common class to inherit from.  This version simplifies what
occurs if both controllers are in the interaction area at the same time.
The answer is that if you inherit from ``ControllerTracker``, you don't
have to worry about it.  The ``OnEnter`` and ``OnLeave`` and all the
``OnXxx`` methods in-between are always called with the same controller.
If the second controller does an action that requires more attention,
like getting a higher priority by ``GetPriority`` or actually pressing
the trigger or grip button, then we first make sure the first controller
first "leaves".  This will not occur if the first controller locked the
area for itself.

``ControllerTracker`` exposes these methods that you can override:

        public virtual void OnMoveOver(Controller controller);
        public virtual void OnTriggerDrag(Controller controller);
        public virtual void OnGripDrag(Controller controller);

Called whenever the controller "moves" (i.e. all the time) inside the
area.  The function that gets called depends on the locking state.  For
example, if you are only interested in "click-and-drag" operations,
override ``OnTriggerDrag`` but not ``OnMoveOver``.  Some sensible ordering
guarantees exist, like that ``OnTriggerDrag`` is always called between
``OnTriggerDown`` and ``OnTriggerUp``.


ConcurrentControllerTracker
+++++++++++++++++++++++++++

When using ``ConcurrentControllerTracker`` as the base class instead of
``ControllerTracker``, you get more flexibility but need to be more
careful.  In this subclass, the ``OnXxx`` methods are called with both
controllers independently.  There is only one generic movement method:

        public virtual void OnMove(Controller[] controllers);

Called with an array of controllers that are inside the area.  If both
controllers are inside, this is called only once with an array of length
two.  Note a potentially useful detail: if one controller leaves the
area, ``OnMove`` is still called once, but with the controller no longer
listed; this occurs in addition to the following ``OnLeave``.

See also ``Controller.GetAdditionalData()``.


GrabbableObject
+++++++++++++++

This is meant as an example inheriting from ``ControllerTracker`` that
you can just drop into any GameObject with a collider.  The object can
then be moved around by pressing the trigger button.  See its source
code as an example of using the ``OnXxx`` methods.



Dialog
++++++

For dialog boxes.  Typically, you'd make the dialog box by creating a
Unity ``Canvas`` component and filling it with dialog items like
``InputField`` or ``Text``.  To make the Canvas usable from BaroqueUI,
you need to change its "Render Mode" to "World Space" and stick an extra
``Dialog`` component in the Canvas.

There are two kinds of dialogs: "pop-ups" show up when the user does
some action to request it; and "pre-positioned" dialogs which are part
of the scene in the first place.  This is the meaning of the
``alreadyPositioned`` check box in the inspector for ``Dialog``.

Note that typically, Canvases are extremely large when compared with the
rest of the scene.  You can ignore that for dialogs that are not
``alreadyPositioned``: they will be scaled down automatically when they
pop up.  For the dialogs that are ``alreadyPositioned``, you need to
scale they them down while positioning them in the first place.

(...)


Popup
+++++

(...)


Menu
++++

(...)



KeyboardClicker
+++++++++++++++

(...)


KeyboardVRInput
+++++++++++++++

(...)

