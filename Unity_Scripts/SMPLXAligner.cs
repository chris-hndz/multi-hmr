using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class SMPLXAligner : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject smplxPrefab;
    public Texture2D backgroundImage;
    public string jsonFilePath;

    public Vector3 manualOffset = new Vector3(0f, -1f, 0f); // Ajusta estos valores según sea necesario
    private GameObject instanciaSmplx;


    private List<GameObject> smplxInstances = new List<GameObject>();
    private string[] _smplxJointNames = new string[] { "pelvis","right_hip","left_hip","spine1","right_knee","left_knee","spine2","right_ankle","left_ankle","spine3", "right_foot","left_foot","neck","right_collar","left_collar","head","right_shoulder","left_shoulder","right_elbow","left_elbow", "right_wrist","left_wrist","right_index1","right_index2","right_index3","right_middle1","right_middle2","right_middle3","right_pinky1","right_pinky2","right_pinky3","right_ring1","right_ring2","left_ring3","right_thumb1","right_thumb2","right_thumb3","left_index1","left_index2","left_index3","left_middle1","left_middle2","left_middle3","left_pinky1","left_pinky2","left_pinky3","left_ring1","left_ring2","right_ring3","left_thumb1","left_thumb2","left_thumb3", "jaw"};
    private Vector3 initialPosition;
    private bool canBeUpdated = false;

    void Start()
    {
        canBeUpdated = false;

        // Configurar la cámara
        mainCamera.orthographic = false;
        mainCamera.fieldOfView = 60f; // Asegúrate de que esto coincida con el FOV usado en demoUnity.py
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.farClipPlane = 1000f;
        
        // Set up background image
        SetupBackgroundImage();
        
        // Load SMPL-X parameters from file
        List<SMPLXParams> smplxParamsList = LoadSMPLXParams(jsonFilePath);
        
        // Create and align SMPL-X instances
        CreateAndAlignSMPLX(smplxParamsList);
    }

    void Update()
    {
        if (canBeUpdated) instanciaSmplx.transform.position = initialPosition + manualOffset;
    }

    void SetupBackgroundImage()
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = new Vector3(0, 0, 10); // Ajusta esto según sea necesario
        
        float aspectRatio = (float)backgroundImage.width / backgroundImage.height;
        float quadHeight = 2f * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * quad.transform.position.z;
        float quadWidth = quadHeight * aspectRatio;
        
        quad.transform.localScale = new Vector3(quadWidth, quadHeight, 1);
        
        Material mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = backgroundImage;
        quad.GetComponent<Renderer>().material = mat;
    }

    List<SMPLXParams> LoadSMPLXParams(string path)
    {
        if (File.Exists(path))
        {
            string jsonContent = File.ReadAllText(path);
            List<SMPLXParams> smplxParamsList = JsonConvert.DeserializeObject<List<SMPLXParams>>(jsonContent);
            Debug.Log("SMPLXParams loaded successfully.");
            return smplxParamsList;
        }
        else
        {
            Debug.LogError("JSON file not found at path: " + path);
            return new List<SMPLXParams>();
        }
    }

    void CreateAndAlignSMPLX(List<SMPLXParams> smplxParamsList)
    {
        foreach (var parames in smplxParamsList)
        {
            GameObject smplxInstance = Instantiate(smplxPrefab);

            smplxPrefab.SetActive(false);
            instanciaSmplx = smplxInstance;

            smplxInstances.Add(smplxInstance);

            // Apply SMPL-X parameters
            ApplySMPLXParams(smplxInstance, parames);

            // Align instance with camera
            AlignWithCamera(smplxInstance, parames);

            initialPosition = instanciaSmplx.transform.position;
            canBeUpdated = true;
        }
    }

    void ApplySMPLXParams(GameObject smplxInstance, SMPLXParams parames)
    {
        SMPLX smplxComponent = smplxInstance.GetComponent<SMPLX>();
        if (smplxComponent != null)
        {
            //smplxComponent.SetPose(parames.pose);
            //smplxComponent.SetShape(parames.shape);
            //smplxComponent.SetExpression(parames.expression);

            //Set Betas
            if (parames.shape != null) SetShape(smplxComponent, parames.shape);

            //Set Pose
            if (parames.pose != null) SetPose(smplxComponent, parames.pose);
            else Debug.Log("parames.pose es null!");
        }
    }

    void AlignWithCamera(GameObject smplxInstance, SMPLXParams parames)
    {
        // Ajustar la posición
        Vector2 normalizedPosition = new Vector2(parames.locationV.x / backgroundImage.width, 
                                                parames.locationV.y / backgroundImage.height);
        Vector3 worldPosition = mainCamera.ViewportToWorldPoint(new Vector3(normalizedPosition.x, 
                                                                            1 - normalizedPosition.y, 
                                                                            parames.distance));
        
        // Ajustar la rotación
        Quaternion worldRotation = Quaternion.Euler(parames.rotationV.x, 
                                                    parames.rotationV.y + 180f, 
                                                    -parames.rotationV.z);

        // Aplicar rotación y posición
        smplxInstance.transform.rotation = worldRotation;
        smplxInstance.transform.position = worldPosition;

        // Aplicar ajustes finos
        smplxInstance.transform.position += smplxInstance.transform.TransformDirection(parames.positionOffsetV);
        smplxInstance.transform.rotation *= Quaternion.Euler(parames.rotationOffsetV);
    }

    private void SetShape(SMPLX smplxMannequin, float[] newBetas)
    {
        for (int i = 0; i < SMPLX.NUM_BETAS; i++)
        {
            smplxMannequin.betas[i] = newBetas[i];
            //Debug.Log("newBetas[" + i + "]: " + newBetas[i]);
        }
        smplxMannequin.SetBetaShapes();

        Debug.Log("Se han aplicado correctamente los blendshapes");
    }

    private void SetPose(SMPLX smplxMannequin, float[][] newPose) 
    {
        Debug.Log("El largo de las poses es de: " + newPose.Length);
        
        /*
        for (int i = 0; i < newPose.Length; i++)
            Debug.Log("newPose[" + i + "][0]: " + newPose[i][0] + " newPose[" + i + "][1]: " + newPose[i][1] + " newPose[" + i + "][2]: " + newPose[i][2]);
        */

        if (newPose.Length != _smplxJointNames.Length)
        {
            Debug.LogError("no tiene el numero correcto de valores " + newPose.Length);
            return;
        }

        return;   
        
    }
}