using UnityEngine;
using UnityEditor;
using NUnit.Framework;


public class ControllerTest
{
    [Test]
    public void TransformFindReturnsInactive()
    {
        GameObject par = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject sub = new GameObject("sub");
        sub.transform.SetParent(par.transform);
        Assert.AreEqual(par.transform.Find("sub"), sub.transform);
        sub.SetActive(false);
        Assert.AreEqual(par.transform.Find("sub"), sub.transform);
    }

    [Test]
    public void UnitTests()
    {
        /* aaAAAhhh tests can't access any fields or methods that are not public, and
         * a namespace declaration doesn't help.  This makes it impossible to write
         * anything more than full integration tests.  As a workaround, unit tests are
         * written like this. */
        BaroqueUI.FakeController.RunUnitTests();
    }
}