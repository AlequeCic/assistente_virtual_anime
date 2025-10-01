using UnityEngine;
using Unity.Cinemachine;
using System.Linq;

public class CameraScroll : MonoBehaviour
{
    public CinemachineCamera mycam;
    [Range(1f, 50f)]
    public float sensitivity = 40;
    public float minzoom = 60;
    public float maxzoom = 20;
    // Controla a velocidade da interpola��o. Valores maiores = mais r�pido.
    public float smoothingFactor = 10f;
    private float nfov;
    void Start()
    {
        // Define o alvo inicial como o FOV atual da c�mera no in�cio
        if (mycam != null)
        {
            mycam.Lens.FieldOfView = minzoom;
            nfov = mycam.Lens.FieldOfView;
        }
    }

    private void Update()
    {
        if (mycam == null)
        {
            Debug.LogWarning("A refer�ncia � CinemachineCamera n�o est� definida no Inspector!");
            return;
        }

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput!= 0) { 
            nfov = nfov - sensitivity * scrollInput;
            nfov = Mathf.Clamp(nfov, maxzoom, minzoom);
        }

        mycam.Lens.FieldOfView = Mathf.Lerp(mycam.Lens.FieldOfView,nfov, Time.deltaTime * smoothingFactor);

    }
}
