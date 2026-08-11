using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using static WeldingPanel;

public class WeldingPanel : MonoBehaviour
{
    /// <summary>Four panel material presets (order matches serialized values 0–3).</summary>
    public enum PanelMaterial
    {
        MildSteel,
        Aluminium,
        StainlessSteel,
        Joints
    }

    /// <summary>Ideal machine settings for a material. Angles are in degrees.</summary>
    [System.Serializable]
    public struct MaterialWeldTargets
    {
        [Tooltip("Target voltage (same units as angletovolt brackets).")]
        public float idealVoltage;
        [Tooltip("Target wire speed (same units as angletowirespeed brackets).")]
        public float idealWireSpeed;
        [Tooltip("Target gas flow (same units as gasflow).")]
        public float idealGasFlow;
        [Tooltip("Used only when this panel has no AngleDisplayEuler; tip vs surface normal vs ideal (°).")]
        public float idealGunToSurfaceAngleDegrees;
        [Tooltip("Allowed ± deviation from ideal voltage.")]
        public float voltageTolerance;
        [Tooltip("Allowed ± deviation from ideal wire speed.")]
        public float wireSpeedTolerance;
        [Tooltip("Allowed ± deviation from ideal gas flow.")]
        public float gasFlowTolerance;
        [Tooltip("Used only when this panel has no AngleDisplayEuler; tip vs surface tolerance (degrees).")]
        public float workAngleToleranceDegrees;
    }

    [Header("Gun angle (Euler)")]
    [Tooltip("If set, weld parameter evaluation uses AngleDisplayEuler.IsGunStraight() (same as \"gun straight\" on the TMP). When null, WeldParameterMonitor falls back to tip vs surface normal if a welding tip is assigned there.")]
    public AngleDisplayEuler angleDisplay;

    [Header("Material (for weld parameter evaluation)")]
    [SerializeField] private PanelMaterial panelMaterial = PanelMaterial.MildSteel;

    [FormerlySerializedAs("steelTargets")]
    [SerializeField] private MaterialWeldTargets mildSteelTargets = new MaterialWeldTargets
    {
        idealVoltage = 21f,
        idealWireSpeed = 250f,
        idealGasFlow = 25f,
        idealGunToSurfaceAngleDegrees = 0f,
        voltageTolerance = 1f,
        wireSpeedTolerance = 50f,
        gasFlowTolerance = 5f,
        workAngleToleranceDegrees = 8f
    };

    [SerializeField] private MaterialWeldTargets aluminiumTargets = new MaterialWeldTargets
    {
        idealVoltage = 19f,
        idealWireSpeed = 200f,
        idealGasFlow = 20f,
        idealGunToSurfaceAngleDegrees = 0f,
        voltageTolerance = 1f,
        wireSpeedTolerance = 50f,
        gasFlowTolerance = 5f,
        workAngleToleranceDegrees = 8f
    };

    [SerializeField] private MaterialWeldTargets stainlessSteelTargets = new MaterialWeldTargets
    {
        idealVoltage = 22f,
        idealWireSpeed = 300f,
        idealGasFlow = 30f,
        idealGunToSurfaceAngleDegrees = 0f,
        voltageTolerance = 1f,
        wireSpeedTolerance = 50f,
        gasFlowTolerance = 5f,
        workAngleToleranceDegrees = 8f
    };

    [SerializeField] private MaterialWeldTargets jointsTargets = new MaterialWeldTargets
    {
        idealVoltage = 21f,
        idealWireSpeed = 250f,
        idealGasFlow = 25f,
        idealGunToSurfaceAngleDegrees = 0f,
        voltageTolerance = 1f,
        wireSpeedTolerance = 50f,
        gasFlowTolerance = 5f,
        workAngleToleranceDegrees = 8f
    };

    [Tooltip("Optional: use this transform's forward as the weld face normal (out of the panel). Gun aim is compared to -forward.")]
    public Transform surfaceNormalReference;

    [SerializeField] private Collider weldingCollider;

    [SerializeField] private Transform[] panels;

    [SerializeField] private Material blobErrorMat, blobGoodMat;

    [SerializeField] private GameObject weldScanner;
    [SerializeField] private int checkTimeSec = 2;
    [SerializeField] private Transform[] checkingTransforms;

    private Transform checkerCapsule;
    private WeldCheckerLight checkerLight;
    private Vector3[] checkingPoints;

    public struct WeldingStats
    {
        public float uniformity;
        public float coveragePercent;
        public float travel;

        public int badweldCount;
        public int holesCount;

    }

    public PanelMaterial GetPanelMaterial() => panelMaterial;

    public void SetPanelMaterial(PanelMaterial material)
    {
        panelMaterial = material;
    }

    public MaterialWeldTargets GetActiveMaterialTargets()
    {
        switch (panelMaterial)
        {
            case PanelMaterial.MildSteel:
                return mildSteelTargets;
            case PanelMaterial.Aluminium:
                return aluminiumTargets;
            case PanelMaterial.StainlessSteel:
                return stainlessSteelTargets;
            case PanelMaterial.Joints:
                return jointsTargets;
            default:
                return mildSteelTargets;
        }
    }

    /// <summary>World-space direction pointing out from the weld face (used for work-angle checks).</summary>
    public Vector3 GetWeldFaceNormalWorld()
    {
        if (surfaceNormalReference != null)
            return surfaceNormalReference.forward.normalized;
        return transform.up.normalized;
    }

    void Awake()
    {
        RebuildCheckingPointsFromTransforms();
    }

    void RebuildCheckingPointsFromTransforms()
    {
        if (checkingTransforms == null || checkingTransforms.Length == 0)
        {
            checkingPoints = System.Array.Empty<Vector3>();
            return;
        }

        var pts = new List<Vector3>();
        for (int i = 0; i < checkingTransforms.Length; i++)
        {
            if (checkingTransforms[i] != null)
                pts.Add(checkingTransforms[i].position);
        }
        checkingPoints = pts.ToArray();
    }

    /// <summary>First non-null checking transform (for scanner pose).</summary>
    Transform GetFirstValidCheckingTransform()
    {
        if (checkingTransforms == null)
            return null;
        for (int i = 0; i < checkingTransforms.Length; i++)
        {
            if (checkingTransforms[i] != null)
                return checkingTransforms[i];
        }
        return null;
    }

    bool HasValidWeldingStatsSetup()
    {
        if (checkingPoints == null || checkingPoints.Length == 0)
            return false;
        if (GetFirstValidCheckingTransform() == null)
            return false;
        if (weldScanner == null)
            return false;
        return true;
    }

    bool isWeldingStatsDone = false;
    WeldingStats weldingStats;
    internal void PopulateWeldingStats(out int delayTimeSec)
    {
        delayTimeSec = checkTimeSec;

        isWeldingStatsDone = false;

        weldingStats = new WeldingStats();

        RebuildCheckingPointsFromTransforms();

        if (!HasValidWeldingStatsSetup())
        {
            Debug.LogWarning("WeldingPanel: assign at least one non-null Checking Transform and Weld Scanner, or the stats scan cannot run.", this);
            delayTimeSec = 0;
            weldingStats.uniformity = 0f;
            weldingStats.coveragePercent = 0f;
            weldingStats.travel = 0f;
            weldingStats.badweldCount = 0;
            weldingStats.holesCount = 0;
            isWeldingStatsDone = true;
            return;
        }

        Transform firstCheck = GetFirstValidCheckingTransform();

        if (checkerCapsule == null)
            checkerCapsule = Instantiate(weldScanner, checkingPoints[0], Quaternion.identity).transform;

        if (checkerLight == null)
            checkerLight = checkerCapsule.GetComponent<WeldCheckerLight>();

        checkerCapsule.rotation = firstCheck.rotation; // Match rotation in case of corner welds needs a bit of tilt.

        weldingStats.uniformity = GetUniformity();
        weldingStats.travel = GetWeldTravelUniformity();

        weldingStats.badweldCount = GetBadWelds();
        weldingStats.holesCount = GetWeldHoles();

        int totalCount = 0;
        int blobCount = 0;

        LeanTween.move(checkerCapsule.gameObject, checkingPoints, checkTimeSec).setOnUpdate((Vector3 positionValue) =>
        {

            bool hasBlob = RaycastCheckWeld(checkerCapsule);
            totalCount++;

            if (hasBlob)
            {
                blobCount++;
                checkerLight.ShowColor(true);
                checkerCapsule.GetComponent<AudioSource>().pitch = 1f;
            }
            else
            {
                checkerCapsule.GetComponent<AudioSource>().pitch = 1.3f;
                checkerLight.ShowColor(false);
            }


        }).setOnComplete(() =>
        {

            if (checkerCapsule)
                Destroy(checkerCapsule.gameObject);

            weldingStats.coveragePercent = (float)blobCount / (float)totalCount;

            isWeldingStatsDone = true;
        });

    }

    internal bool GetWeldResults(out WeldingStats stats)
    {
        stats = weldingStats;
        return isWeldingStatsDone;
    }

    private bool RaycastCheckWeld(Transform checkPos)
    {
        bool hasBlob = false;

        Vector3 checkPosWithGap = checkPos.position + Vector3.up * 0.1f;

        if (Physics.Raycast(checkPosWithGap, Vector3.down, out RaycastHit hit))
        {
            if (hit.transform.gameObject.layer == 6) //Hits welding blob.
            {
                hasBlob = true;
                //Debug.DrawRay(checkPosWithGap, Vector3.down, Color.green, 100);
            }
            else
            {
                hasBlob = false;
                //Debug.DrawRay(checkPosWithGap, Vector3.down, Color.red, 100);
            }

        }

        return hasBlob;
    }

    //Blobs not in contact with welding line.
    private int GetBadWelds()
    {
        int badWeldsCount = 0;

        if (panels == null || weldingCollider == null)
            return 0;

        foreach (Transform panel in panels)
        {
            if (panel == null)
                continue;
            WeldingBlobSet[] blobs = panel.GetComponentsInChildren<WeldingBlobSet>();

            foreach (WeldingBlobSet blob in blobs)
            {
                //Change to Weld Panel Layer, to not get counted by coverage detection.
                blob.gameObject.layer = 7; 

                //Delay change color for effect
                LeanTween.value(0, 1, checkTimeSec).setOnComplete(() =>
                {
                    blob.GetComponent<Renderer>().material = blobErrorMat;
                });
            }


            badWeldsCount += blobs.Length;
        }

        //Good welds
        WeldingBlobSet[] goodBlobs = weldingCollider.transform.GetComponentsInChildren<WeldingBlobSet>();

        foreach (WeldingBlobSet blob in goodBlobs)
        {
            //Delay change color for effect
            LeanTween.value(0, 1, checkTimeSec).setOnComplete(() =>
            {
                blob.GetComponent<Renderer>().material = blobGoodMat;
            });
        }

        return badWeldsCount;
    }

    private int GetWeldHoles()
    {
       
        GameObject[] holeObjects = GameObject.FindGameObjectsWithTag("WeldHole");
        int holesCount = holeObjects.Length;

        return holesCount;

    }

    private float GetUniformity()
    {
        float uniformity = 0.0f;

        float smallestScale = Mathf.Infinity;
        float largestScale = 0;

        GameObject[] weldObjects = GameObject.FindGameObjectsWithTag("WeldObject");
        foreach (GameObject obj in weldObjects)
        {
            if (obj == null)
                continue;
            if(obj.transform.localScale.x < smallestScale)
                smallestScale = obj.transform.localScale.x;

            if(obj.transform.localScale.x > largestScale)
                largestScale = obj.transform.localScale.x;


        }

        if (largestScale <= 0f || float.IsInfinity(smallestScale))
            return 0f;

        uniformity = ((smallestScale + largestScale) / 2)/largestScale;

        return uniformity;
    }

    //Weld Travel
    List<float> weldTravels = new List<float>();
    internal void AddWeldTravel(float weldTravel)
    {
        weldTravels.Add(weldTravel);
    }
    internal void ResetWeldTravel()
    {
        weldTravels.Clear();
    }

    private float GetWeldTravelUniformity()
    {
        if (weldTravels.Count <= 10)
            return 0;


        float idealTime = 0.419f; //Ideal time for each blob to form before making another.

        float averageTime = weldTravels.Average();

        float travelPerf = 1 - (Mathf.Abs(idealTime - averageTime) / idealTime);


        //Debug.Log("GetWeldTravelPerformance: averageTime = " + averageTime);

        return travelPerf;
    }
}
