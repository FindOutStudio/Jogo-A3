using Unity.Cinemachine;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake instance;
    [SerializeField] private float StrongShakeForce = 1f;
    [SerializeField] private float MediumShakeForce = 0.6f;
    [SerializeField] private float WeakShakeForce = 0.1f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public void StrongCameraShaking(CinemachineImpulseSource impulseSource)
    {
        impulseSource.GenerateImpulseWithForce(StrongShakeForce);
    }

    public void MediumCameraShaking(CinemachineImpulseSource impulseSource)
    {
        impulseSource.GenerateImpulseWithForce(MediumShakeForce);
    }
    public void WeakCameraShaking(CinemachineImpulseSource impulseSource)
    {
        impulseSource.GenerateImpulseWithForce(WeakShakeForce);
    }
}
