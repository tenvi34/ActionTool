using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionController : MonoBehaviour
{
    public List<ActionScript> MyActions;
    public ActionScript currentAction;
   
    public void FireAction(int index)
    {
        if (currentAction)
        {
            Destroy(currentAction);
            currentAction = null;
        }
        
        currentAction = Instantiate(MyActions[index], transform.position, transform.rotation);
    }
}
