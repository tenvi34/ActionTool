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
            Destroy(currentAction.gameObject);
            currentAction = null;
        }
        
        currentAction = Instantiate(MyActions[index], transform.position, transform.rotation);
        currentAction.SetActionController(this);
        currentAction.StartAction();
    }
}
