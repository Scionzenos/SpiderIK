#if(UNITY_EDITOR)
using UnityEngine;
using UnityEditor;
#pragma warning disable IDE0090 // Use 'new(...)'
// FIK functions in VRC when uploaded with FIK 2.1, script inserts values properly as well
public class SpiderIK : MonoBehaviour
{
    public int pointSelectionInt, setupInt;

    // Always enabled
    [Tooltip("The root of your avatar, where the Avatar Descriptor is located")]
    public GameObject avatar;
    [Tooltip("The number of pairs of legs that your avatar has, this value needs to be set so that the scripts have correctly sized arrays (Spiders have 4 pairs of legs, quadrupeds have 2 pairs of legs)")]
    public int pairsOfLegs = 4;
    [Tooltip("The height that a leg reaches when taking a step, the step is actually a graph. The value you set is the mid point of a bell curve.")]
    public float stepHeight;

    // For use when the "Single Point" section is enabled
    [Tooltip("What the legs will overall follow, for a drider, this would be the humanoid hips, for Doctor Octopus this would be the humanoid chest, ect.")]
    public Transform constrainToWhat;
    // For use when the "Multiple Points" section is enabled
    [Tooltip("The array of constraints that the legs will follow. The first pair of legs will attach to the first element, the second pair to the second element, ect.")]
    public Transform[] constraintArray;
    // For use when the "Only Legs" section is enabled
    [System.Serializable]
    public class LegArray
    {
        public Transform leftLeg;
        public Transform rightLeg;
    }
    [Tooltip("The array of constraints that the legs will follow. The first pair of legs will attach to the first element, the second pair to the second element, ect.")]
    public LegArray[] legArray;
    //public Transform[] legArray;

    // Organizational Gameobjects
    private UnityEngine.Animations.ConstraintSource constraintSource;
    private GameObject grounder, executionOrderObj, ikRoot, constraint, vrikRoot, fabrikRoots, vrikTargets;

    // Execution Order
    private RootMotion.FinalIK.IKExecutionOrder exec;

    // Grounder and FABRIK
    private GameObject legLeftFAB, legRightFAB, legLeftFABRoot, legRightFABRoot;
    private RootMotion.FinalIK.FABRIK legLeftFABRIK, legRightFABRIK;
    private RootMotion.FinalIK.FABRIKRoot legLeftFABRIKRoot, legRightFABRIKRoot;
    private RootMotion.FinalIK.GrounderIK grounderIK;
    private float grounderStepHeight;

    // VRIK
    private GameObject VRIKObj, headTarget;
    RootMotion.FinalIK.VRIK VRIK;

    //Error Checking
    private int errorCount = 0;

    private UnityEngine.Animations.ParentConstraint parentConstraint;
    private Transform armature;

    protected void OnDrawGizmosSelected()
    {
        // Draw a flat plane that shows the step height
        Gizmos.color = new Color(1, 0, 0, 0.8f);
        if (legLeftFAB != null && legRightFAB != null && avatar != null)
        {
            float legDistance = Vector3.Distance(legLeftFAB.transform.position, legRightFAB.transform.position) * 1.5f;
            Gizmos.DrawCube(new Vector3(avatar.transform.position.x, avatar.transform.position.y + stepHeight, avatar.transform.position.z), new Vector3(legDistance, 0.0001f, legDistance));
        }
        else
        {
            Gizmos.DrawCube(new Vector3(0, stepHeight, 0), new Vector3(3, 0.0001f, 3));
        }
    }

    private void CheckForProjectErrors()
    {
        errorCount = 0;
        if (!AssetDatabase.IsValidFolder("Assets/Plugins/RootMotion"))
        {
            Debug.LogError("The RootMotion folder from Final IK is not located at Assets/Plugins/RootMotion. While this doesn't matter in the editor, the IK will not import with the avatar into VRChat.");
            errorCount += 1;
        }
    }

    private void InstantiateObjects(GameObject avatar, int pairsOfLegs, Transform constrainToWhat,int constraintNumType)
    {
        // Remove the old IK if it existed
        if (ikRoot != null){ DestroyImmediate(ikRoot); }
        // Ik Root
        ikRoot = new GameObject("Inverse Kinematics");
        ikRoot.transform.parent = avatar.transform;

        // Execution Order
        executionOrderObj = new GameObject("Execution Order");
        executionOrderObj.transform.parent = ikRoot.transform;
        exec = executionOrderObj.AddComponent<RootMotion.FinalIK.IKExecutionOrder>();
        exec.IKComponents = new RootMotion.FinalIK.IK[pairsOfLegs * 5];                       // You need 5 scripts per pair of legs

        // This function is only called once a spider is created. This works for single constrain setups
        // Multi constraint setups are built in another function that is called once per loop instead
        // Single Constraint Setup
        if (constraintNumType == 0)
        {
            constraint = new GameObject("Parent Constraint");
            constraint.transform.parent = ikRoot.transform;
            constraint.transform.position = constrainToWhat.position;
            parentConstraint = constraint.AddComponent<UnityEngine.Animations.ParentConstraint>();
            constraintSource = new UnityEngine.Animations.ConstraintSource { weight = 1, sourceTransform = constrainToWhat };
            parentConstraint.AddSource(constraintSource);
            parentConstraint.constraintActive = true;
            parentConstraint.locked = true;

            // FABRIK Roots
            fabrikRoots = new GameObject("FABRIK Roots");
            fabrikRoots.transform.parent = constraint.transform;

            // VRIK Targets
            vrikTargets = new GameObject("VRIK Targets");
            vrikTargets.transform.parent = constraint.transform;
        }

        // VRIK
        vrikRoot = new GameObject("VRIK");
        vrikRoot.transform.parent = ikRoot.transform;

        // Grounder
        grounder = new GameObject("Grounder");
        grounder.transform.parent = ikRoot.transform;
        grounderIK = grounder.AddComponent<RootMotion.FinalIK.GrounderIK>();
        grounderIK.legs = new RootMotion.FinalIK.IK[pairsOfLegs * 2];
        grounderIK.pelvis = constrainToWhat;
        grounderIK.characterRoot = avatar.transform;

        grounderIK.solver.layers = LayerMask.GetMask("Default", "Environment");
        grounderIK.solver.maxStep = 0.5f;
        grounderIK.solver.footSpeed = 2.5f;
        grounderIK.solver.footRadius = 0.001f;
        grounderIK.solver.footRotationWeight = 0;
        grounderIK.solver.footRotationSpeed = 0;
        grounderIK.solver.maxFootRotationAngle = 0;
        grounderIK.solver.pelvisSpeed = 0;
        grounderIK.solver.lowerPelvisWeight = 0;
        grounderIK.solver.liftPelvisWeight = 0;
    }

    private Transform FindVrikBone(string childName, Transform parent, string[] targets,string endString)
    {
        Transform child = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            for (int j = 0; j < targets.Length; j++)
            {
                if (parent.GetChild(i).gameObject.name.ToLower().Contains(targets[j])) { child = parent.GetChild(i); break; }
            }
        }
        if (child == null)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).gameObject.name.ToLower().EndsWith(endString)) { child = parent.GetChild(i); break; }
            }
        }
        if (child == null)
        {
            Debug.LogError("Could not find valid [" + childName + "] child for [" + parent.gameObject.name + "]. Make sure the naming follows usual conventions.");
        }
        return child;
    }

    private void InstantiateVRIKSkeletonFromHip(Transform hip,int pairNum)
    {
        // Check for existance of spine
        if (hip.Find("Spine " + pairNum) != null)
        {
            Debug.LogWarning("'Hips only' was selected but spine was detected on " + hip.gameObject.name + ". Creation of VRIK rig for leg pair " + pairNum + " has been aborted.");
            return;
        }

        float offset = 0.3f;
        GameObject spine = new GameObject();
        InstantiateChildBone(hip.gameObject, spine, new Vector3(0,offset,0),pairNum,"Spine ");

        GameObject head = new GameObject();
        InstantiateChildBone(spine, head, new Vector3(0, offset, 0), pairNum, "Head ");

        // Left Arm
        GameObject leftArm = new GameObject();
        InstantiateChildBone(spine, leftArm, new Vector3(-offset, 0, 0), pairNum, "Left Arm ");

        GameObject leftElbow = new GameObject();
        InstantiateChildBone(leftArm, leftElbow, new Vector3(-offset, 0, 0), pairNum, "Left Elbow ");

        GameObject leftWrist = new GameObject();
        InstantiateChildBone(leftElbow, leftWrist, new Vector3(-offset, 0, 0), pairNum, "Left Wrist ");

        // Right Arm
        GameObject rightArm = new GameObject();
        InstantiateChildBone(spine, rightArm, new Vector3(offset, 0, 0), pairNum, "Right Arm ");

        GameObject rightElbow = new GameObject();
        InstantiateChildBone(rightArm, rightElbow, new Vector3(offset, 0, 0), pairNum, "Right Elbow ");

        GameObject rightWrist = new GameObject();
        InstantiateChildBone(rightElbow, rightWrist, new Vector3(offset, 0, 0), pairNum, "Right Wrist ");
    }
    private void InstantiateChildBone(GameObject parent,GameObject child, Vector3 offset, int pairNum, string namePrefix)
    {
        child.name = (namePrefix + pairNum);
        child.transform.parent = parent.transform;
        child.transform.position = parent.transform.position + offset;
    }

    public void CreateSpider(int setupType, int constraintNumType)
    {
        armature = avatar.transform.Find("Armature");

        // Create objects that need to exist before the loops start
        InstantiateObjects(avatar,pairsOfLegs,constrainToWhat,constraintNumType);
        CheckForProjectErrors();
        if (errorCount == 1) { 
            Debug.LogWarning(errorCount + " error was found in pre-initialization, please check the console for more information."); 
        }
        else if (errorCount > 1)
        {
            Debug.LogWarning(errorCount + " errors were found in pre-initialization, please check the console for more information.");
        }

        for (int pair = 1; pair <= pairsOfLegs; pair++)
        {
            // Create a unique constraint for each pair of legs
            if (constraintNumType == 1)
            {
                constraint = new GameObject("Parent Constraint " + pair);
                constraint.transform.parent = ikRoot.transform;
                constraint.transform.position = constraintArray[pair - 1].position;
                parentConstraint = constraint.AddComponent<UnityEngine.Animations.ParentConstraint>();
                constraintSource = new UnityEngine.Animations.ConstraintSource { weight = 1, sourceTransform = constraintArray[pair-1] };
                parentConstraint.AddSource(constraintSource);
                parentConstraint.constraintActive = true;
                parentConstraint.locked = true;

                // FABRIK Roots
                fabrikRoots = new GameObject("FABRIK Roots " + pair);
                fabrikRoots.transform.parent = constraint.transform;

                // VRIK Targets
                vrikTargets = new GameObject("VRIK Targets " + pair);
                vrikTargets.transform.parent = constraint.transform;
            }
            // Create the VRIK script
            VRIKObj = new GameObject("Pair " + pair + " VRIK");
            VRIKObj.transform.parent = vrikRoot.transform;
            VRIK = VRIKObj.AddComponent<RootMotion.FinalIK.VRIK>();

            // Define the VRIK skeleton
            Transform hips, spine, head, armL, elbowL, wristL, armR, elbowR, wristR, legAL, legBL, legCL, legDL, legAR, legBR, legCR, legDR;
            if (setupType == 2)
            {
                GameObject hipsObj = new GameObject();
                hips = hipsObj.transform;
                hipsObj.name = "Hips " + pair;
                hips.parent = avatar.transform.Find("Armature");
                hips.position = Vector3.Lerp(legArray[pair-1].leftLeg.transform.position, legArray[pair-1].rightLeg.transform.position, 0.5f);
            }
            // Find the skeleton needed for VRIK and FABRIK
            hips = armature.Find("Hips " + pair);
            if (hips == null)
            {   // Error case for bad naming of hips or too many pairs input
                Debug.LogError("Hips " + pair + " was not found, make sure that the naming scheme follows [Hips #] and that you have input the correct amount of pairs of legs for your avatar.");
                return;
            }
            // If we are on the "Hips only" or "Only Legs" mode, we can create the rest of the VRIK here before we try to find it all
            
            if (setupType >= 1) { InstantiateVRIKSkeletonFromHip(hips, pair); }
            spine = hips.Find("Spine " + pair);
            if (spine == null)
            {
                string[] spineTargets = { "spine", "chest", "hip" };
                spine = FindVrikBone("Spine", hips, spineTargets,null);
            }
            // Only Legs mode
            if (setupType == 2)
            {
                legArray[pair-1].leftLeg.transform.parent = hips;
                legArray[pair-1].rightLeg.transform.parent = hips;
            }
            head = spine.Find("Head " + pair);
            if (head == null)
            {
                string[] headTargets = { "head", "neck" };
                head = FindVrikBone("Head", spine, headTargets, null);
            }

            armL = spine.Find("Left arm " + pair);
            if (armL == null)
            {
                string[] armLTargets = { "left"};
                armL = FindVrikBone("Left arm", spine, armLTargets, "l");
            }
            elbowL = armL.GetChild(0);
            wristL = elbowL.GetChild(0);

            armR = spine.Find("Right arm " + pair);
            if (armR == null)
            {
                string[] armRTargets = { "right"};
                armR = FindVrikBone("Right arm", spine, armRTargets, "r");
            }
            elbowR = armR.GetChild(0);
            wristR = elbowR.GetChild(0);

            legAL = hips.Find("Leg AL " + pair);
            if (legAL == null)
            {
                string[] legLTargets = { "left"};
                legAL = FindVrikBone("Left leg", hips, legLTargets, "l");
            }
            legBL = legAL.GetChild(0);
            legCL = legBL.GetChild(0);
            legDL = legCL.GetChild(0);

            legAR = hips.Find("Leg AR " + pair);
            if (legAR == null)
            {
                string[] legRTargets = { "right"};
                legAR = FindVrikBone("Right leg", hips, legRTargets, "r");
            }
            legBR = legAR.GetChild(0);
            legCR = legBR.GetChild(0);
            legDR = legCR.GetChild(0);

            // Define VRIK references
            VRIK.references.root = hips;
            VRIK.references.pelvis = hips;
            VRIK.references.spine = spine;
            VRIK.references.head = head;
            VRIK.references.leftUpperArm = armL;
            VRIK.references.leftForearm = elbowL;
            VRIK.references.leftHand = wristL;
            VRIK.references.rightUpperArm = armR;
            VRIK.references.rightForearm = elbowR;
            VRIK.references.rightHand = wristR;
            VRIK.references.leftThigh = legAL;
            VRIK.references.leftCalf = legBL;
            VRIK.references.leftFoot = legCL;
            VRIK.references.leftToes = legDL;
            VRIK.references.rightThigh = legAR;
            VRIK.references.rightCalf = legBR;
            VRIK.references.rightFoot = legCR;
            VRIK.references.rightToes = legDR;

            // Create and place the VRIK Head Target
            headTarget = new GameObject("Head Target " + pair);
            headTarget.transform.parent = vrikTargets.transform;
            headTarget.transform.position = head.position;

            // Set VRIK parameters for spider specific functionality
            // Isolate these values as they are only available in FIK 1.9+
            try {
                VRIK.solver.LOD = 1;
                VRIK.solver.leftArm.shoulderTwistWeight = 0;
                VRIK.solver.rightArm.shoulderTwistWeight = 0;
            }
            catch { Debug.Log("Final IK Version is outdated, please use versions 1.9+"); }
            //VRIK.solver.LOD = 1;
            VRIK.solver.plantFeet = false;

            VRIK.solver.spine.headTarget = headTarget.transform;
            VRIK.solver.spine.minHeadHeight = 0;
            VRIK.solver.spine.bodyPosStiffness = 1;
            VRIK.solver.spine.bodyRotStiffness = 1;
            VRIK.solver.spine.neckStiffness = 1;
            VRIK.solver.spine.rotateChestByHands = 0;
            VRIK.solver.spine.chestClampWeight = 0;
            VRIK.solver.spine.headClampWeight = 0;
            VRIK.solver.spine.moveBodyBackWhenCrouching = 0;
            VRIK.solver.spine.maintainPelvisPosition = 0;
            VRIK.solver.spine.maxRootAngle = 0;

            VRIK.solver.leftArm.positionWeight = 0;
            VRIK.solver.leftArm.rotationWeight = 0;
            VRIK.solver.leftArm.shoulderRotationWeight = 0;
            //VRIK.solver.leftArm.shoulderTwistWeight = 0;
            VRIK.solver.leftArm.palmToThumbAxis = new Vector3(1, 0, 0);

            VRIK.solver.rightArm.positionWeight = 0;
            VRIK.solver.rightArm.rotationWeight = 0;
            VRIK.solver.rightArm.shoulderRotationWeight = 0;
            //VRIK.solver.rightArm.shoulderTwistWeight = 0;
            VRIK.solver.rightArm.palmToThumbAxis = new Vector3(1, 0, 0);

            VRIK.solver.locomotion.footDistance = (Mathf.Abs(legCL.position.x) + Mathf.Abs(legCR.position.x)) / 2; // average
            VRIK.solver.locomotion.stepThreshold = (Mathf.Abs(legCL.position.x) + Mathf.Abs(legCR.position.x)) / 3;
            VRIK.solver.locomotion.maxVelocity = 1;
            VRIK.solver.locomotion.maxLegStretch = 0.9f;
            VRIK.solver.locomotion.rootSpeed = 400;
            VRIK.solver.locomotion.stepHeight = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, stepHeight), new Keyframe(1, 0));
            VRIK.solver.locomotion.offset = new Vector3(0, 0, -1 * (hips.position.z - legDL.position.z));

            // Excess Leg Length
            // Calculate the amount of bent leg by subtracting the total length of the leg by the shortest possible leg from point A to D
            // The extra distance that isn't the shortest possible distance is the extra leg that can bend to accomodate upwards and downwards movement
            float hypotenuse = Vector3.Distance(legAL.position, legDL.position);
            float legLength = Vector3.Distance(legAL.position, legBL.position) + Vector3.Distance(legBL.position, legCL.position) + Vector3.Distance(legCL.position, legDL.position);
            float excessLegLength = legLength - hypotenuse;
            grounderStepHeight += excessLegLength;


            // Define the legs as FABRIK bones
            RootMotion.FinalIK.IKSolver.Bone legALBone = new RootMotion.FinalIK.IKSolver.Bone();
            RootMotion.FinalIK.IKSolver.Bone legARBone = new RootMotion.FinalIK.IKSolver.Bone();
            RootMotion.FinalIK.IKSolver.Bone legBLBone = new RootMotion.FinalIK.IKSolver.Bone();
            RootMotion.FinalIK.IKSolver.Bone legBRBone = new RootMotion.FinalIK.IKSolver.Bone();
            RootMotion.FinalIK.IKSolver.Bone legCLBone = new RootMotion.FinalIK.IKSolver.Bone();
            RootMotion.FinalIK.IKSolver.Bone legCRBone = new RootMotion.FinalIK.IKSolver.Bone();
            RootMotion.FinalIK.IKSolver.Bone legDLBone = new RootMotion.FinalIK.IKSolver.Bone();
            RootMotion.FinalIK.IKSolver.Bone legDRBone = new RootMotion.FinalIK.IKSolver.Bone();

            // Define the left leg in FABRIK
            legLeftFAB = new GameObject("Leg " + pair + " Left FABRIK");
            legLeftFAB.transform.parent = grounder.transform;                                   // Set the FABRIK as a child of the Grounder
            legLeftFAB.transform.position = legDL.position;                                     // Position the FABRIK at the end of the leg for clarity
            legLeftFABRIK = legLeftFAB.AddComponent<RootMotion.FinalIK.FABRIK>();               // Create the FABRIK script and place it on the gameobject
            legLeftFABRIK.solver.bones = new RootMotion.FinalIK.IKSolver.Bone[4];               // Create an array of 3 points for the bones
            legALBone.transform = legAL;
            legBLBone.transform = legBL;
            legCLBone.transform = legCL;
            legDLBone.transform = legDL;
            legLeftFABRIK.solver.bones.SetValue(legALBone, 0);
            legLeftFABRIK.solver.bones.SetValue(legBLBone, 1);
            legLeftFABRIK.solver.bones.SetValue(legCLBone, 2);
            legLeftFABRIK.solver.bones.SetValue(legDLBone, 3);
            grounderIK.legs.SetValue(legLeftFABRIK, pair - 1);                                  // Put the left FABRIK into the Grounder

            // Define the right leg in FABRIK
            legRightFAB = new GameObject("Leg " + pair + " Right FABRIK");
            legRightFAB.transform.parent = grounder.transform;                                  // Set the FABRIK as a child of the Grounder
            legRightFAB.transform.position = legDR.position;                                    // Position the FABRIK at the end of the leg for clarity
            legRightFABRIK = legRightFAB.AddComponent<RootMotion.FinalIK.FABRIK>();             // Create the FABRIK script and place it on the gameobject
            legRightFABRIK.solver.bones = new RootMotion.FinalIK.IKSolver.Bone[4];              // Create an array of 3 points for the bones
            legARBone.transform = legAR;
            legBRBone.transform = legBR;
            legCRBone.transform = legCR;
            legDRBone.transform = legDR;
            legRightFABRIK.solver.bones.SetValue(legARBone, 0);
            legRightFABRIK.solver.bones.SetValue(legBRBone, 1);
            legRightFABRIK.solver.bones.SetValue(legCRBone, 2);
            legRightFABRIK.solver.bones.SetValue(legDRBone, 3);
            grounderIK.legs.SetValue(legRightFABRIK, pairsOfLegs + pair - 1);                      // Put the right FABRIK into the Grounder

            // Define the left FABRIK Root
            legLeftFABRoot = new GameObject("Leg " + pair + " Left FABRIK Root");
            legLeftFABRoot.transform.parent = fabrikRoots.transform;
            legLeftFABRoot.transform.position = legAL.position;
            legLeftFABRIKRoot = legLeftFABRoot.AddComponent<RootMotion.FinalIK.FABRIKRoot>();
            legLeftFABRIKRoot.solver.chains = new RootMotion.FinalIK.FABRIKChain[1];            // Define array
            legLeftFABRIKRoot.solver.chains[0] = new RootMotion.FinalIK.FABRIKChain {ik = legLeftFABRIK, pin = 0, pull = 0 };

            // Define the right FABRIK Root
            legRightFABRoot = new GameObject("Leg " + pair + " Right FABRIK Root");
            legRightFABRoot.transform.parent = fabrikRoots.transform;
            legRightFABRoot.transform.position = legAR.position;
            legRightFABRIKRoot = legRightFABRoot.AddComponent<RootMotion.FinalIK.FABRIKRoot>();
            legRightFABRIKRoot.solver.chains = new RootMotion.FinalIK.FABRIKChain[1];            // Define array
            legRightFABRIKRoot.solver.chains[0] = new RootMotion.FinalIK.FABRIKChain {ik = legRightFABRIK, pin = 0, pull = 0};          
            // Set values in the Execution order
            exec.IKComponents.SetValue(VRIK, pair - 1);
            exec.IKComponents.SetValue(legLeftFABRIK, pairsOfLegs + pair - 1);
            exec.IKComponents.SetValue(legRightFABRIK, pairsOfLegs * 2 + pair - 1);
            exec.IKComponents.SetValue(legLeftFABRIKRoot, pairsOfLegs * 3 + pair - 1);
            exec.IKComponents.SetValue(legRightFABRIKRoot, pairsOfLegs * 4 + pair - 1);

        }
        grounderIK.solver.maxStep = grounderStepHeight / 4; // Set the grounder Max Step to the average excess leg length
    }

    public void PrepareUpload()
    {
        // Redefine and find the ikRoot again incase of cache clearing problems (restart unity or scene inbetween creation and fixing)
        GameObject ikRoot = null;
        object[] objectsInScene = FindObjectsOfType(typeof(GameObject));
        foreach (object o in objectsInScene)
        {
            GameObject g = (GameObject)o;
            if (g.GetComponent<RootMotion.FinalIK.IKExecutionOrder>() != null && g.transform.parent.parent.gameObject == avatar)
            {
                ikRoot = g.transform.parent.gameObject;
            }
        }
        // Find the root of all the VRIKs in the expected position
        if (ikRoot.transform.Find("VRIK") == null)
        {
            Debug.LogError("Could not find parent of all VRIKs called 'VRIK', don't rename objects for no reason");
            return;
        }
        GameObject vrikRoot = ikRoot.transform.Find("VRIK").gameObject;
        if (ikRoot.name == "Inverse Kinematics (Ready for Upload)") { Debug.LogError("Model has already been prepared for upload"); return; }
        for (int i = 0; i < vrikRoot.transform.childCount; i++)
        {
            RootMotion.FinalIK.VRIK vrikChild = vrikRoot.transform.GetChild(i).GetComponent<RootMotion.FinalIK.VRIK>();
            vrikChild.solver.locomotion.stepThreshold *= 3;
            vrikChild.solver.locomotion.stepSpeed /= 2;

            vrikChild.name += " [Slowed]";
        }
        ikRoot.name = "Inverse Kinematics (Ready for Upload)";
    }
}
#endif 