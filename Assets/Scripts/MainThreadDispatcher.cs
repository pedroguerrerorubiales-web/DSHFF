using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Permite ejecutar acciones en el hilo principal de Unity desde hilos secundarios (red, etc.).
/// Añádelo a un GameObject que esté siempre activo en la escena.
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    static readonly Queue<Action> _queue = new Queue<Action>();

    public static void Enqueue(Action action)
    {
        lock (_queue) _queue.Enqueue(action);
    }

    void Update()
    {
        while (_queue.Count > 0)
        {
            Action action;
            lock (_queue) action = _queue.Dequeue();
            action();
        }
    }
}
