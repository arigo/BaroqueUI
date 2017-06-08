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

Preliminary but usable.  APIs may change in the future, and more will be
added, but always keeping in mind the ideas and goals outlined above.

For Unity 5.5 and Unity 5.6 (other versions not tested but may work).
Developed for HTC Vive.


Contact
-------

Main page and development version: https://github.com/arigo/BaroqueUI

Main developer: Armin Rigo <arigo@tunes.org>.

Contributions welcome!


Demo
----

This repo is meant to be checked out inside the ``Assets/`` directory of
a Unity project.  This project should have SteamVR installed too, e.g.
from the Assets Store.  (On Unity 5.6, BaroqueUI will patch SteamVR at
runtime to work around an issue that prevents SteamVR's controllers from
showing up.)

A demo scene with multiple interaction places is present in the
``Examples/Scenes/`` folder.



Programming overview
--------------------

All classes are in the BaroqueUI package, which you can access with
``using BaroqueUI;``.  Some classes (but not all of them) are components
that install themselves automatically at runtime, but there is no need
to add them in the editor.

In a few steps:

* You drag and drop the ``Teleport Enabler`` prefab in the scene (from
  (``BaroqueUI/Prefabs``) if you want the default behavior of enabling
  teleporting to move around.  This is optional.

* For each "interactable" object, you make sure it has got a collider
  and then attach a script to it.

* In the ``Start()`` method:

        var ct = Controller.HoverTracker(this);
        ct.onTriggerDown += OnTriggerDown;

* This minimal example will make BaroqueUI call your ``OnTriggerDown``
  method whenever the trigger button is pressed.  You can use "tab" or
  "ctrl+dot" in Visual Studio to insert the method automatically with
  the correct signature.  See below for details.

* The BaroqueUI framework also provides classes to display a keyboard
  and interact with dialog-box-like displays.



Baroque
-------

The class ``Baroque`` contains only static methods.  The most important ones
are:

* ``GetControllers()`` returns an array of ``Controller`` objects.
  This static method is also available on the ``Controller`` class.
  See below.

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
        public float triggerVariablePressure;   // between 0 and 1
        public bool touchpadPressed;   // big round area
        public bool gripPressed;       // side buttons
        public bool menuPressed;       // top small button
        public bool GetButton(EControllerButton btn);

Check whether individual buttons are pressed or not.

        public bool touchpadTouched;
        public Vector2 touchpadPosition;

Check whether the finger is touching the touchpad and where (X and Y
coordinates between -1 and 1).

        public MonoBehaviour CurrentHoverTracker();

Return which "tracker" (see below) the controller is currently over.

        public void GrabHover(bool active);

Locks or unlocks the current hover tracker.  As long as it is locked,
the controller cannot "leave".  It will be considered to be inside the
zone of that same tracker until at least ``GrabHover(false)`` is called.

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
index inside the ``Controller.GetControllers()`` array.

		public static Controller GetController(int index);
		public static Controller[] GetControllers();

Return the controller by index, or the list of all controllers (same as
``Baroque.GetControllers()``).

        public T GetAdditionalData<T>(ref T[] locals) where T: new();

Attach additional local, controller-specific data.  To use this, you
pass by reference an array of some type ``T``, which should be a small
local class.  The array does not need to be initialized; this method
does it for you.  It returns the ``index``th item of the array,
instantiating it if it is still null.


Controller Hover trackers
-------------------------

For every object in the scene that you want to interact with, you need a
script that is registered with the controllers.  We call the script a
"tracker".  Typically, registration is done in ``Start()`` after setting
up the MonoBehaviour:

        var ct = Controller.HoverTracker(this);
		ct.onEnter += ...;
		ct.onLeave += ...;
		ct.onTriggerDown += ...;

You also need to make sure the GameObject or its children contain at least
one collider, typically of the "trigger" kind.  This is used to know the
maximal interaction area.

The fields of the 'ct' object are mostly C# events, which you set with 
``+=``.  We will discuss them in the following sections.  A few non-event
fields first:

		ct.computePriority = ...;
		ct.SetPriority(float value)
		ct.SetPriorityFromDistance(float maximum);

The "priority" is used to pick trackers in case there are overlapping
colliders.  The highest "priority" value is choosen first.  The priority
can be computed by assigning a delegate to ``computePriority``, taking a
controller argument and returning a float.  Or, use ``SetPriority()`` to
give the tracker a constant priority; or ``SetPriorityFromDistance()`` to
have it compute the priority dynamically from the distance to the collider.
In the last case, the priority returned is equal to ``maximum`` minus the
distance.  The default computation method is ``SetPriorityFromDistance(0)``,
which returns a (usually small) negative number.

Note that the distance is computed to a point in the "core" of the collider,
not the surface.  For cubic BoxColliders, for example, it is the center.
See the details in ``Collider.DistanceToColliderCore()``.

If the returned priority is ``float.NegativeInfinity``, then the controller
is considered completely outside the tracker.

		ct.isConcurrent

When this is ``false`` (the default), then BaroqueUI takes care for you
that only one controller can be interacting with the given tracker, not
both at the same time.  It simplifies the logic you need and avoids bugs
due to a rarely-tested use case.  If you say ``ct.isConcurrent = true``,
then you must handle the case that events might be called for both 
controllers concurrently.


Global trackers
---------------

In addition to "Hover trackers" described above, you can also register
global trackers.  These receive events not otherwise handled.  These
trackers are always tried after the hover trackers.  They can also have
priorities to differenciate among them.  These global trackers should be
installed on GameObjects with no colliders, and the default priority is simply 0.

They are installed with:

        var gt = Controller.GlobalTracker(this);


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
the controllers, or when it used to touch it just before.  It is passed
as argument an array of controllers that are touching now.  (That means
that after both controllers leave a tracker, ``OnControllersUpdate`` is
called once more with an empty array.)


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
ways it can be interacted with.  We can touch the touchpad.  Once
touched, we can actually press the touchpad, or not.  It is also
possible to move the finger on the touchpad.

So far, BaroqueUI will interpret all this as one action among three
possible ones:

1. If you press the touchpad, you get ``OnTouchPressDown``,
   ``OnTouchPressDrag`` and ``OnTouchPressUp``, which work like the
   other ``OnXxxDown/OnXxxDrag/OnXxxUp`` events.

2. If you touch the touchpad and immediately move your finger,
   you get ``OnTouchScroll`` events.

3. Otherwise, if you touch the touchpad you get ``OnTouchDown``,
   ``OnTouchDrag`` and ``OnTouchUp`` events.

Only one of these actions is possible at a given time.  The touchpad
works in BaroqueUI like a state machine with up to 7 states, depending
on which methods are actually implemented.  The full state machine is
detailled below.  Some states don't exist if some actions are not
implemented.  To get the most "uncooked" events, implement only action 3
and poll inside ``OnTouchDrag`` for the other conditions.


    <outside the tracker> -----------------------.
         |                                       | enter when touching
         | enter when not touching               |
         |                                       |    .---------------------.
         |                                       v    v                     |
         | .----------------------------- <dead touching>                   |
         v v        untouching     `\          |   |  \                     |
    <released>                      |          |   |  <out>                 |
         |    \                     |          |   |            un-pressing |
         |     <out>            .--------------'   '-------.                |
         |                      |   |                      |                |
         | touching             |   |                      |                |
         v                      |   |                      |                |
    <small delay> ----------------------------------------\|                |
         | |                    |   |                      +                |
         | `-------------------\|   '---------.   .-------\| pressing       |
         |                      +              \ /         +                |
         |        moving finger |               |          v                |
         |                      |               |       <action 1> ---------'
         | default              v               |
         v                   <action 2>         |
     <action 3>                 |               |
         |                      \.              |
         '--------------------------------------'

The "dead touching" state is if we have the touchpad touched but didn't
actually start touching it just now and have no other state for it.

The "small delay" state selects between the three action states.  There
is no delay if only one action state is actually implemented in the
tracker.  The "default" path is followed if the small delay elapses, if
we move the controller enough in space, or if we "press" and there is no
"action 1" implemented; or if we untouch the touchpad quickly (in the
last case, it will leave "action 3" immediately afterwards, but that's
still registered as a tap).

If "action 1" is not implemented, pressing doesn't cause state changes.
If "action 2" is not implemented, moving the finger doesn't cause state
changes.



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
