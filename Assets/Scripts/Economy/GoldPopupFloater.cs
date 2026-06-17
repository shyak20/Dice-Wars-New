using UnityEngine;

/// <summary>Optional component on gold popup prefab: drifts upward and scales out.</summary>
public class GoldPopupFloater : MonoBehaviour
{
    private float _life;
    private float _speed;
    private float _elapsed;

    public void Begin(float lifetime, float floatSpeed)
    {
        _life = lifetime;
        _speed = floatSpeed;
        Destroy(gameObject, lifetime + 0.1f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        transform.position += Vector3.up * (_speed * Time.deltaTime);
        var t = _life > 0 ? Mathf.Clamp01(_elapsed / _life) : 1f;
        transform.localScale = Vector3.one * (1f + 0.15f * t);
    }
}
