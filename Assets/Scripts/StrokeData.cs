using System;
using UnityEngine;

/// <summary>
/// Datos de un trazo de anotación. Se serializa a JSON para enviarse por UDP
/// desde el PC del profesor a las Oculus Quest del alumno.
/// </summary>
[Serializable]
public class StrokeData
{
    public Vector3[] points; // Posiciones locales relativas al labAnchor
    public Color     color;
    public float     width;
    public string    id;     // GUID único por trazo
}
