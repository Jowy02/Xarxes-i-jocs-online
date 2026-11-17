using UnityEngine;

[DisallowMultipleComponent]
public class PrometeoSkinManager : MonoBehaviour
{
    // Skin configuration
    [Header("Available Car Skins")]
    [SerializeField] private Texture2D[] playerTextures = new Texture2D[12];

    // Body configuration
    [Header("Car Body Renderer")]
    [SerializeField] private Renderer bodyRenderer;
    [Tooltip("Index of the body material in the Renderer materials list")]
    [SerializeField] private int bodyMaterialIndex = 0;

    // Material properties
    private MaterialPropertyBlock propertyBlock;
    private static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexID = Shader.PropertyToID("_MainTex");

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (bodyRenderer == null)
        {
            Debug.LogError("[SKIN] Body Renderer has not been assigned on " + gameObject.name);
        }
    }

    // Apply player skin
    public void ApplySkin(int playerId)
    {
        if (playerTextures == null || playerTextures.Length == 0)
        {
            Debug.LogWarning("[SKIN] No player textures configured");
            return;
        }

        if (bodyRenderer == null)
        {
            Debug.LogWarning("[SKIN] Body Renderer is missing");
            return;
        }

        // Select texture based on player ID
        int textureIndex = Mathf.Abs(playerId % playerTextures.Length);
        Texture2D selectedTexture = playerTextures[textureIndex];

        if (selectedTexture == null)
        {
            Debug.LogWarning("[SKIN] Missing texture at index " + textureIndex);
            return;
        }

        // Get the materials assigned to the body renderer
        Material[] materials = bodyRenderer.sharedMaterials;

        if (bodyMaterialIndex < 0 || bodyMaterialIndex >= materials.Length)
        {
            Debug.LogError("[SKIN] Invalid body material index");
            return;
        }

        Material bodyMaterial = materials[bodyMaterialIndex];

        if (bodyMaterial == null)
        {
            Debug.LogError("[SKIN] Body material is missing");
            return;
        }

        // Detect the texture property used by the shader
        int textureProperty;

        if (bodyMaterial.HasProperty(BaseMapID))
        {
            textureProperty = BaseMapID;
        }
        else if (bodyMaterial.HasProperty(MainTexID))
        {
            textureProperty = MainTexID;
        }
        else
        {
            Debug.LogError("[SKIN] Shader does not support _BaseMap or _MainTex");
            return;
        }

        // Preserve other existing property overrides
        bodyRenderer.GetPropertyBlock(propertyBlock, bodyMaterialIndex);

        // Apply the selected texture only to this car
        propertyBlock.SetTexture(textureProperty, selectedTexture);
        bodyRenderer.SetPropertyBlock(propertyBlock, bodyMaterialIndex);

        Debug.Log("[SKIN] Applied skin " + textureIndex + " to " + gameObject.name);
    }
}