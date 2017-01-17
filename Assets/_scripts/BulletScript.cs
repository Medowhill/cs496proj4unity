using System.Threading;
using UnityEngine;

public class BulletScript : MonoBehaviour
{
    public int m_LifeTime;

    private bool m_Destroy;
    private Rigidbody bullet;

    // Use this for initialization
    void Start()
    {
        bullet = GetComponent<Rigidbody>();
        new Thread(() =>
        {
            Thread.Sleep(m_LifeTime);
            m_Destroy = true;
        }).Start();
    }

    private void Update()
    {
        if (m_Destroy)
        {
            Destroy(bullet.gameObject);
            m_Destroy = false;
        }
    }
}
