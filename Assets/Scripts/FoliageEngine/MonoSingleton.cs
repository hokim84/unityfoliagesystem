using UnityEngine;

public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static object m_Lock = new object();
    private static T m_Instance;

    public static T Instance
    {
        get
        {
            lock (m_Lock)
            {
                if (m_Instance == null)
                {
                    // Search for existing instance.
                    m_Instance = (T)FindFirstObjectByType(typeof(T));

                    // Create new instance if one doesn't already exist.
                    if (m_Instance != null)
                    {
                        return m_Instance;
                    }

                    // Need to create a new GameObject to attach the singleton to.
                    var singletonObject = new GameObject();
                    m_Instance = singletonObject.AddComponent<T>();
                    singletonObject.name = $"@{typeof(T).ToString()}";

                    // Make instance persistent.
                    DontDestroyOnLoad(singletonObject);
                }

                return m_Instance;
            }
        }
    }

    private void OnApplicationQuit()
    {
        OnRelease();
    }


    protected virtual void OnRelease() {}
}