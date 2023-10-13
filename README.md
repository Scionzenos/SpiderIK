Spider IK by Scionzenos from https://github.com/Scionzenos/SpiderIK

If something is not working, make sure the console is not printing any errors.

If you run into any problems, join the discord to ask for help!
https://discord.gg/g7rQEGk

=== Optimal Naming Scheme ===
The Optimal Naming Scheme picture is the way the script expects the rig to be named. 
There are catches for naming that is not that, so being exact isnt important, but if your naming scheme matches it then there shouldnt be any problems on that front at least.
Dashes mean that the name doesn't matter, though having multiple children under something that usually only has 1 child may cause issues.

Changelog:

Spider IK v1.0.8  || Current
- Added support for constraint targets that are not pointed upwards with no rolls
- A couple more checks for hip naming schemes (hips #,hip #)
- Check for legs via postion offset from hips if naming scheme is too weird to find via bone name
  
Spider IK v1.0.7
- Added warning console message if code guessed where the parent of the hips were
- Renamed "Constrain To What" to "Constraint Target" because the original name bothered me
- Added a manual override for finding the parent of the spider hips for single source spiders (IE the hips still need to be under 1 parent)
- Added an image that represents the optimal naming scheme for the script, or basically what it expects
- Added a readme for patchnotes and links

Spider IK v1.0.6
- Redid armature detection system to account for bad naming

Spider IK v1.0.5
- Fixed null pointer error when automatic VRIK rig generation attempts end name checks
- Added error checks for if the VRIK legs have less than 4 bones
- Add warning message for when there are more than 2 bones under the VRIK hips, which could cause problems with automatic leg finding depending on naming

Spider IK v1.0.3
- Multi hip setup now sets a pelvis target in the grounder

Spider IK v1.0.2
- Removed error case for bone count due to incorrect documentation

Spider IK v1.0.1
- Foot distance and step threshold no longer assume that the avatar is at 0,0,0
- Removed FIK 1.9+ reliant code, older versions of FIK should function the same now
- Added an error check for optional VRIK bones instead of random Null error
- Added check for if the VRIK skeletons are there or not to automatically select "Only hips" even when "Existing VRIK" is selected
