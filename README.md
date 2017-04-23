BaroqueUI
=========

A Unity package giving a VR "application" user interface for the HTC Vive.


Basic idea
----------

When the two controllers move around and their buttons are pressed, we try to generate "events" in a way that is similar to desktop window managers, as well as JavaScript in web pages---and different from how games are typically approached.

The hope is that it should give a better basis for VR applications that are not meant to be games.


Status
------

Very very preliminary.  For Unity 5.5 (other versions not tested but may work).


Demo
----

Download this repo, open ``BaroqueUI_Demo`` in Unity, and add SteamVR from the Asset Store.  For example scene(s) see ``Assets/Scenes``.

* Everything is done by putting a few "Action" objects on "Controller (left)" and "Controller (right)" gameobjects.  These two are inside the "[CameraRig]" object standard with SteamVR.

* In the demo, we have a ``Teleport Action`` subobject on ``Controller (right)``.  This makes the standard teleporter beam when we press the touchpad on the right controller.  You can tweak the Transform of this subobject to change the direction of the beam (in the demo it is shooted 10 degrees upward, hence ``Rotation: X: -10``), as well as the exact point the beam starts from.  A good way to select that point is to run and pause the game, zoom in on the actual controller object, and select the corresponding ``Teleport Action`` object.  Then you see and can move/rotate the regular red-green-blue arrows (I recommend local coordinates in Unity for that: the 7th button on the top row in Unity 5.5 should say "Local" instead of "Global").  Once you are happy, copy the ``Transform`` component to the clipboard (right-click in the Inspector on the title bar ``Transform``); leave the game execution mode (un-click the Play button); and paste the values back to the same ``Transform`` component (same right-click, ``Paste Component Values``).

* The ``Teleport Action`` object has got a ``TeleportAction`` component: in the Inspector you see notably ``Controller Button``, which says which of the buttons should do the action.

* The demo has also a ``Scene Action`` object with a ``SceneAction`` component: it sends the events to the object in the scene that touch the ``SceneAction`` gameobject, in this case a small red blinking sphere (with a SphereCollider; if none are found we use simply ``Transform.position``).  The scene objects themselves install event handlers to receive the actions, e.g. in ``Start()``.

* ``BaroqueUI_Grabbable`` is an example of a script put in the three small Spheres.  Often the scripts put in the scene don't come from BaroqueUI at all, but in this case ``BaroqueUI_Grabbable`` implements grabbing and moving around the object.  See documentation in this script.

* ``BaroqueUI_Controller`` is the main controlling script, but it installs itself automatically on the "Controller (left)" and "Controller (right)" objects.


Programming overview
--------------------

The goal is to handle events from the controllers.  Here, we simplified the buttons into five per HTC Vive controller: trigger pressed (the trigger is the button below the controller); touchpad pressed (clicks on the big round area); touchpad touched (when we only touch it; useful for tracking movements on the touchpad location); grib button (on the sides of the controller); and menu button (small button on top).

Each event is sent to scripts that are on the "Controller (left)" and "Controller (right)" gameobjects inside the "[CameraRig]" object standard with SteamVR.  These scripts inherit from ``AbstractControllerAction`` (a subclass of ``MonoBehavior``) and can be placed either directly on the ``Controller`` or on some sub-GameObject if you need more control.  In addition to being placed inside either "Controller (left)" or "Controller (right)", each such script is configured with a ``Controller Button``, so that it handles exactly one of the 2x5 buttons.

Two important examples of ``AbstractControllerAction`` subclasses:

* ``TeleportAction``: draws a teleport beam when the button is down, and moves the player at the target when the button is released.

* ``SceneAction``: looks for objects in the scene and forwards the actions to them if they touch.

We assume the following model for each of the 2x5 buttons independently: each button can be "not over anything", "over something" (but the button is not pressed), or "grabbing something" (with the button pressed).

For each non-pressed button, each frame, we look for something that we are over by calling the FindHover() method on all Actions corresponding to that button.  If there are multiple results, we keep the "highest priority" one.  We call OnButtonEnter and OnButtonLeave when the result changes.

When we press the button, OnButtonDown is called.  At that point the button state goes to "grabbed": we don't call FindHover() any more, but instead we call OnButtonMove every frame, and finally OnButtonUp.

These ``OnButtonXxx`` are virtual methods on the ``Hover`` instance returned by ``FindHover()``.

``Hover`` is a base class that can be subclassed.  It may represent a gameobject that we are hovering over, but not necessarily.  For actions like ``TeleportAction`` there is just one Hover which is returned when we press the button only (no entering/leaving).  On the other extreme, you can create Hovers that specify a position more precisely than with just a gameobject, e.g. as a particular vertex of a rendered mesh.

The logic behind OnButtonEnter and OnButtonLeave is based on the identity of the Hover object: as long as the same Hover object is returned by FindHover(), we are in the same area.  The two approaches here are to either prebuild the Hover object(s) and return one of them (or none) from FindHover(), or to use a more dynamic caching technique.

Hover instances are sorted by comparing them.  The default comparison on the Hover base class uses the value of the field "priority".

Each ``SceneAction`` has got a ``sceneActionName``, usually set in the Inspector.  To register scene objects with a particular ``SceneAction`` their script needs to call the static method ``SceneAction.RegisterXxx("scene_action_name", gameObject, xxx)``.  The ``gameObject`` identifies the object whose colliders we consider.  Variants, from most basic to most flexible:

* ``SceneAction.RegisterClick(..., OnClick)``: will only call the OnClick method when clicked.

* ``SceneAction.RegisterClick(..., OnClick, reverse_priority)``: same, but with an explicit reverse_priority (a float, by default zero; higher values mean that the Hover will be considered afterwards; negative values are also allowed).

* ``SceneAction.Register(..., hover_instance)``: gives a single fixed Hover.  Will invoke the methods OnButtonXxx() on it.  (If needed, the priority can be set by changing the field ``reverse_priority`` in the Hover instance.)

* ``SceneAction.Register(..., OnFindHover)``: will call ``OnFindHover()`` to figure out which hover to use.


Here is an example of using multiple SceneAction objects for the same button.  Say we want to be able to grab an object A with a button, but also if we are close (but not touching) and press the same button, a menu for object A opens.  To implement this, first put a SceneAction with name "My Grabber", and give the object a BaroqueUI_GrabbableObject script with the same name "Grab".  Then put a second SceneAction with name "My Menu".  Put this second SceneAction on a subobject of the Controller and give this subobject a large-ish SphereCollider.  The large SphereCollider makes the action detect objects that are farther away.  Finally, on a custom script of object A, we call 
``SceneAction.RegisterClick("My Menu", gameObject, OnClick, 10)``.  The large value of ``reverse_priority``, arbitrarily set to 10 here, gives that registration lower priority; the other Hover objects, like the ones built for the "Grab" SceneAction, will take priority.