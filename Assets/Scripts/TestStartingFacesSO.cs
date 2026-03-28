using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TestStartingFaces", menuName = "DiceGame/TestStartingFaces")]
public class TestStartingFacesSO : ScriptableObject
{
    public bool isActive;
    public bool changeAll;
    public List<DieFaceSO> testFaces = new List<DieFaceSO>();
}
