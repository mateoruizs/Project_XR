using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class ScreenOrbitFacingCamera : MonoBehaviour
{
    [Header("References")]
    public Transform userCamera;                 
    public bool invertForward = false;

    [Header("Orbit")]
    public bool lockDistanceToCamera = true;     
    public float minPlanarDistance = 0.05f;

    [Header("Rotation")]
    public float maxDegreesPerSecond = 0f; // 0 = instantáneo

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grab;
    UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor _interactor;
    Transform _attach;
    float _lockedDistance;

    // 👇 nuevo: offset angular para evitar el “snap” al agarrar
    Quaternion _dirOffset = Quaternion.identity;

    Rigidbody _rb;

    void Awake()
    {
        _grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        _grab.selectEntered.AddListener(OnSelectEntered);
        _grab.selectExited.AddListener(OnSelectExited);

        _rb = GetComponent<Rigidbody>();
    }

    void OnDestroy()
    {
        _grab.selectEntered.RemoveListener(OnSelectEntered);
        _grab.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        _interactor = args.interactorObject;
        _attach = _interactor.GetAttachTransform(_grab);

        var cam = userCamera != null ? userCamera : Camera.main?.transform;
        if (cam == null || _attach == null) return;

        var camPos = cam.position;

        // distancia fija actual (para que no cambie al agarrar)
        _lockedDistance = Vector3.Distance(transform.position, camPos);

        // 👇 calcula el offset para que en el primer frame NO haya salto
        Vector3 handDir0 = (_attach.position - camPos).normalized;
        Vector3 screenDir0 = (transform.position - camPos).normalized;

        if (handDir0.sqrMagnitude > 0.0001f && screenDir0.sqrMagnitude > 0.0001f)
            _dirOffset = Quaternion.FromToRotation(handDir0, screenDir0);
        else
            _dirOffset = Quaternion.identity;

        // Para evitar “throw”/inercia rara al soltar luego
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        _interactor = null;
        _attach = null;

        // deja la pantalla EXACTAMENTE donde estaba (sin inercia)
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    void LateUpdate()
    {
        if (_interactor == null || _attach == null) return;

        var cam = userCamera != null ? userCamera : Camera.main?.transform;
        if (cam == null) return;

        Vector3 camPos = cam.position;
        Vector3 handVec = _attach.position - camPos;

        if (handVec.magnitude < minPlanarDistance)
            return;

        Vector3 targetPos;

        if (lockDistanceToCamera)
        {
            Vector3 handDir = handVec.normalized;

            // 👇 usa el offset para mantener la “misma zona” de la esfera al agarrar
            Vector3 screenDir = (_dirOffset * handDir).normalized;

            targetPos = camPos + screenDir * _lockedDistance;
        }
        else
        {
            targetPos = _attach.position;
        }

        transform.position = targetPos;

        // Mirar a cámara sin roll
        Vector3 toCam = camPos - transform.position;
        if (toCam.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(toCam, Vector3.up);
        if (invertForward) look *= Quaternion.Euler(0f, 180f, 0f);

        if (maxDegreesPerSecond <= 0f)
            transform.rotation = look;
        else
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, maxDegreesPerSecond * Time.deltaTime);
    }
}
