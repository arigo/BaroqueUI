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

This repo is meant to be checked out inside the ``Assets/`` directory of a
Unity project.  This project should have SteamVR installed too, e.g. from
the Assets Store.

For example scenes, see the ``Examples/`` folder.  (Attribution: the scene
layout comes from one of the example scenes in the VRTK project.)



Programming overview
--------------------

All classes are in the BaroqueUI package (which you can access with
``using BaroqueUI;``).  A few classes are components that install
themselves automatically at runtime, but there is no need to add them 
in the editor.

In a few steps:

* You drag and drop the ``Teleport Enabler`` prefab in the scene (from
  (``BaroqueUI/Prefabs``) if you want the default behavior of enabling
  teleporting to move around.

* For each "interactable" object, you make sure it has got a collider
  and then attach a script to it.

* In the ``Start()`` method, you call ``Controller.Register(this)``.

* The BaroqueUI framework will call the ``OnXxx`` methods when the
  controller interacts with the object.  See below for details.

* The BaroqueUI framework also provides classes to display a keyboard
  and interact with dialog-box-like displays.

(...write more here...)



BaroqueUIMain
-------------

This class contains only static methods.  The most important ones are:

* ``GetControllers()`` returns an array of ``Controller`` objects.  See below.

* ``GetHeadTransform()`` returns the Transform of the headset.

* ``FindPossiblyInactive(string path_in_scene)``: finds an object by path in
  the scene (use ``/`` to return subobjects).  This function will also return
  objects that are disabled, which is what you need to find a dialog "prefab"
  stored in the scene.


Controller
----------

``Controller`` is a component that installs itself on the ``Controller
(left)`` and ``Controller (right)`` objects in ``SteamVR``.  The general
public interface is:

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

        public bool triggerPressed;    // bottom of the controller
        public bool touchpadPressed;   // big round area
        public bool gripPressed;       // side buttons
        public bool menuPressed;       // top small button
        public bool GetButton(EControllerButton btn);

Check whether individual buttons are pressed or not.

        public Touchpad touchpad;

Check whether the finger is touching the touchpad and where,
and contains logic to interpret finger movements.  See below.

        public MonoBehaviour HoverControllerTracker();
        public MonoBehaviour GrabbedControllerTracker()

Return which "controller tracker" (see below) the controller
is currently over, or is currently grabbed (i.e. locked inside).

        public void GrabFromScript(MonoBehaviour locked);

Locks the "tracker", or unlocks it if ``null``.

        public void HapticPulse(int durationMicroSec = 500);

Send a haptic pulse to the controller.

        public Transform SetPointer(string pointer_name);
        public Transform SetPointer(GameObject prefab);

Set the "mouse pointer".  This is a small object that is visible in
front of the controller, centered at the point that is at
``Controller.position``.  If given by name, it must be a GameObject from
a ``Resources`` directory, in a subdirectory ``Pointers``.  Call with
an empty string to remove the pointer.  The result is the transform of
the pointer object (or null).

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
index inside the ``BaroqueUIMain.GetControllers()`` array.

        public T GetAdditionalData<T>(ref T[] locals) where T: new();

Attach additional local, controller-specific data.  To use this, you
pass by reference an array of some type ``T``, which should be a small
local class.  The array does not need to be initialized; this method
does it for you.  It returns the ``index``th item of the array,
instantiating it if it is still null.


Controller trackers
-------------------

For every object in the scene that you want to interact with, you need a
script that is registered with the controllers.  We call the script a
"tracker".  Typically, registration is done in ``Start()`` after setting
up the MonoBehaviour:

        Controller.Register(this);

With the exception of "global trackers" (see below), you also need to make
sure the GameObject or its children contain at least one collider, typically
of the "trigger" kind.  This is used to know the maximal interaction area.

The signature of the Register() static method is:

        static void Register(MonoBehaviour tracker,
                             float priority = 0.0f,
							 bool concurrent = false);

		delegate float GetPriorityDelegate(Controller controller);
		static void Register(MonoBehaviour tracker,
                             GetPriorityDelegate priority,
							 bool concurrent = false);

The "priority" is used to pick trackers in case there are overlapping
colliders.  The priority is given to trackers where "priority" is highest.
Typically, the delegate version of "priority" is used to vary the priority
based on the exact controller position.  The default is a delegate that
computes a negative priority, equal to minus the distance between the
controller and a "core point" (like the collider box or sphere's center) or
"core segment" (like the collider capsule's central axis).  If the returned
priority is ``float.NegativeInfinity``, then the controller is considered
completely outside the tracker.

If the tracker is registered with ``concurrent: false`` (the default),
then BaroqueUI takes care for you that only one controller can be
interacting with the given tracker, not both at the same time.  It
simplifies the logic you need and avoids bugs due to a rarely-tested use
case.  If you say ``concurrent: true``, then you must handle the case
that events might be called for both controllers concurrently.


Event sets
----------

Controller events are organized in sets which can be handled or not (by
implementing or not the corresponding methods).

The sets are: hovering; clicking the trigger; clicking the grip button;
interacting with the touchpad; and clicking the menu button.

Each event is sent to the first tracker that handles the set, in some
order.  The order is mainly defined by the priority of each tracker,
with some exceptions noted below.


OnControllersUpdate
-------------------

The special method ``OnControllersUpdate`` (note the plural) is called
on all trackers that define it, either when the tracker touches one of
the controllers, or used to touch it just before.  It is passed as
argument an array of controllers that are touching now.  That means that
after both controllers leave a tracker, ``OnControllersUpdate`` is
called once more with an empty array.


Hovering event set
------------------

Methods ``OnEnter``, ``OnMoveOver``, ``OnLeave``.  Each one
takes a Controller as argument (like all remaining OnXxx methods).
Called when the controller enters the tracker's zone, stays in it, and
leaves it.

If this event set is implemented, the other event sets below are only
called between ``OnEnterXxx`` and ``OnLeave``.

If this event set is not implemented and the tracker's GameObject has no
trigger, then it is a global tracker.  It can handle the other sets of
events, but in the priority queue, it is put behind all non-global
trackers, and so will only receive events from sets not handled by
non-global trackers.


Clicking event sets
-------------------

If the tracker does not implement the hovering event set, then we use
the usual priority rules for the event sets below.  If the tracker does
implement the hovering event set, then the following event sets are
given the topmost priority as long as this tracker is the current
hovering tracker, and are never called if this tracker is not the
current hovering tracker.

Trigger button (below the controller): ``OnTriggerDown``,
``OnTriggerDrag``, ``OnTriggerUp``.  Between ``OnTriggerDown`` and
``OnTriggerUp``, the general ``OnMoveOver`` event is not sent.

Grip button (on the sides): ``OnGripDown``, ``OnGripDrag``,
``OnGripUp``.  Note that BaroqueUI detects when both grip and trigger is
pressed and ignores the second one.  Between ``OnGripDown`` and
``OnGripUp``, the general ``OnMoveOver`` event is not sent.

Touchpad (big round area): see below.

Menu button (small button above the touchpad): ``OnMenuClick``.


Touchpad
--------

The touchpad has got a more complicated event set to handle the various
ways it can be interacted with.

We can press the touchpad.  This generates ``OnTouchpadDown``,
``OnTouchpadDrag``, ``OnTouchpadUp``.  Like above, ``OnMoveOver`` is
not sent between ``OnTouchpadDown`` and ``OnTouchpadUp``.

On the other hand, we can touch the touchpad without pressing.  As long
as we do it, plus exactly when we stop, the ``OnTouchpadTouching`` event
is generated.  This event is sent in addition to ``OnMoveOver``, if the
tracker is a hover tracker, and not at all if an ``OnXxxDrag`` is being
sent.

If the receiver needs to interpret the finger movements, it can use the
``controller.touchpad`` instance, with the following properties:

        public bool touched;
        public Vector2 position;

Whether we are touching the touchpad now, and the position of the finger
on it (X and Y coordinates between -1 and 1).

        public bool is_scrolling;
        public Vector2 scroll;

Whether we are scrolling (moving the finger across the surface), and if
so, by how much since the last controller update.  This is true only if
we move the finger by a threshold amount immediately after we touched
the touchpad, without moving much the controller itself.

        public bool is_tapping;

Whether we are tapping (touching without scrolling).  This is true only
if we know that ``is_scrolling`` will not become true during this touch.
It really means "is touching the touchpad, not pressing it so far, and
definitely not scrolling".  Note that ``is_tapping`` can be true for a
long time.  On the other hand, if we touch briefly the touchpad and
release it before the end of the small delay, then ``is_tapping`` will
be true at the release, for the minimal period of time of a single
controller-update tick.



GrabbableObject
---------------

This is meant as an example of tracker script that you can just drop
into any GameObject with a collider.  The object can then be moved
around by pressing the trigger button.  See its source code as an
example of using the ``OnXxx`` methods.



Dialog
------

For dialog boxes.  Typically, you'd make the dialog box by creating a
Unity ``Canvas`` component and filling it with UI widgets like
``InputField`` or ``Text``.  To make the Canvas usable from BaroqueUI,
you need to change its "Render Mode" to "World Space" and stick an extra
``Dialog`` component in the Canvas.

There are two kinds of dialogs: "pop-ups" show up when the user does
some action to request it; and "pre-positioned" dialogs which are part
of the scene in the first place.  This is the meaning of the
``alreadyPositioned`` check box in the inspector for ``Dialog``.

        public Dialog MakePopup(Controller controller, GameObject requester = null)

This method of Dialog objects is used for pop-ups.  It duplicates the
GameObject associated with the Dialog, and position it appropriately for
the controller.  If 'requester' is not null, we use that as the
"attached" object; otherwise, we use the original Dialog object
directly.  The "attached" object is only used when asking for a pop-up
twice: if you ask again for a pop-up with the same attached object, then
the second time is interpreted as a request to close it, and the
MakePopup method will return ``null`` in this case.

Note that when you make them in Unity, Canvases are extremely large when
compared with the rest of the scene.  You can ignore that for dialogs
that are not ``alreadyPositioned``: they will be scaled down
automatically by MakePopup().  For the dialogs that are
``alreadyPositioned``, you need to scale them down while positioning
them in the first place.

Dialog objects have these additional methods to read or write the value
displayed by UI widgets:

        public T Get<T>(string widget_name);
        public void Set<T>(string widget_name, T value,
                           UnityAction<T> onChange = null);

Reads or writes the value in the widget with the give name (the name
of the corresponding GameObject).  The type ``T`` must be of a type
appropriate for the widget type.  Currently supported:

* Text: reads or writes a string
* InputField: reads or writes a string
* Slider: reads or writes a float
* Toggle (checkboxes): reads or writes a bool
* Dropdown: reads or writes an integer (the index of the selected item)

The optional ``onChange`` function is called when the value is changed
by the user.  Note that the next call to ``Set<T>()`` removes the
previously set ``onChange`` callback; it must be specified in all calls
to ``Set<T>()`` to remain in effect.  If given as ``null``, it is simply
removed.

For pop-up dialogs, you need to call ``Set<T>()`` on the copy returned
by ``MakePopup()``.

For buttons, you need this variant, with no value and an argument-less
callback:

        public void SetClick(string clickable_widget_name, UnityAction onClick);

For dropdown lists that you need to populate from the script (as opposed to
having it pre-populated in the inspector), use this method in addition to
``Set<int>()``:

        public void SetChoices(string choice_widget_name, List<string> choices);



Menu
----

For menus.  Usage is:

        var menu = new Menu {
            { "Red", () => mat.color = Color.red},
            { "Green", () => mat.color = Color.green},
            { "Blue", () => mat.color = new Color(0.25f, 0.35f, 1)},
            { "White", () => mat.color = Color.white},
        };
        menu.MakePopup(controller, gameObject);


KeyboardClicker, KeyboardVRInput
--------------------------------

For keyboards.  Mostly, it should show up automatically on InputFields
from dialog boxes.  To add a keyboard manually into the scene, look into
the Prefabs.
