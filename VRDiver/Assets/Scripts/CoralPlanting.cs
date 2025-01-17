using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(AudioSource))]
// Represents the action of planting a coral by forcing the player to hold a coral fragment inside the planting spot for a certain amount of time before the action is considered complete. The action can be interrupted and restored at a later time.
public class CoralPlanting : MonoBehaviour
{
    #region Variables
    public static Dictionary<CoralTypes, int> coralsCollection = InitDict(); //stores planted corals amounts.
    public static event Action<Dictionary<CoralTypes, int>> OnPlantingActionComplete;

    [Tooltip("Time in seconds that a coral fragment being held must remain inside the trigger for the planting action to be completed")]
    [SerializeField] float secondsToCompleteAction = 5f;

    [Tooltip("The transform for the planted coral")]
    [SerializeField] Transform CoralAttachPoint;

    [Tooltip("Coral objects of type Actioner start the planting action")]
    [SerializeField] CoralTypes Actioner;

    [Header("Sound & Visual effects")]
    [SerializeField] AudioClip actionCompleteSFX;
    [SerializeField] AudioClip plantingSFX;

    [Tooltip("A visual cue that indicates how much time is left to complete the action.")]
    [SerializeField] ProgressBar progressBar;

    [SerializeField] Material incorrectTypeMaterial;
    [SerializeField] Material correctTypeMaterial;
    [SerializeField] Material defaultMaterial;
    [Tooltip("The object that indicates which type of coral goes in this planting spot")]
    [SerializeField] MeshRenderer PlantingZoneMesh;


    private IEnumerator plantingActionCheck; //defines the coroutine in which the planting action is checked for execution.
    private float coroutineTimeStep = 0.1f; //The time rate at which the executeAction coroutine runs.
    private float secondsLeftToCompleteAction;//stores the time left to complete the planting action as the action progresses.
    private GameObject coralValidAtSpot; //a reference to the first coral occupying this coral planting spot.
    private AudioSource audioSource;
    #endregion

    #region BuiltInMethods
    private void Awake()
    { //Initialize components and get neccesary references.
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = plantingSFX; //assign an audioclip to avoid null reference errors later.
        plantingActionCheck = PlantingActionCheckCoroutine();
        secondsLeftToCompleteAction = secondsToCompleteAction;
    }

    private void OnTriggerEnter(Collider other)
    {
        //Check if what enters is a coral
        if (other.TryGetComponent<Coral>(out Coral enteringCoral))
        {
            //Check if the coral entering is of a valid type, and make it occupy this planting slot.
            if (IsTheRightCoral(enteringCoral))
            {
                coralValidAtSpot = enteringCoral.gameObject;

                //Check if the coral occupying this spot is being held, and start the plantingActionCheck coroutine.
                if (coralValidAtSpot.GetComponent<MyGrabable>().IsHeld)
                {
                    StopCoroutine(plantingActionCheck);
                    StartCoroutine(plantingActionCheck);
                }
            }
        }

        //Alternatively, start the plantingActionCheckCoroutine, if the player enters while a valid coral is inside the spot.
        else if (coralValidAtSpot != null && other.CompareTag("Player"))
        {
            StopCoroutine(plantingActionCheck);
            StartCoroutine(plantingActionCheck);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //Check if what exits is a coral
        if (other.TryGetComponent<Coral>(out Coral quittingCoral))
        {
            //case1: //Check that the coral spot is empty (only valid corals can occupy the spot), and return the color to default.
            if (coralValidAtSpot == null)
            {
                PlantingZoneMesh.material = defaultMaterial;
                return;
            } 

            // case2: //Check that the coral quitting is the same as the coral ocuppying the spot, and stop coroutine.
            if (quittingCoral.gameObject.GetInstanceID()==coralValidAtSpot.gameObject.GetInstanceID())
            {
                coralValidAtSpot = null; //coralspot turns free.
                PlantingZoneMesh.material = defaultMaterial;//planting zone material turns default (blue).
                InterruptAction(); //i.e. stop feedback sound
                StopCoroutine(plantingActionCheck); //stop plantingActionCheck coroutine.
                return;
            }

            //case3: a valid coral remains occupying the slot, the material remains of the correct type (green).
            if(coralValidAtSpot !=null)
            {
                PlantingZoneMesh.material = correctTypeMaterial;
                return;
            }
            
            //Last option: there is not a valid coral inside the spot remaining, but another coral is there. //CHECK THIS LOGIC, NOT CURRENTLY EXECUTING.
            else PlantingZoneMesh.material = incorrectTypeMaterial;
        }

        //Alternatively, check is what exists is the player while a valid coral remains, and  stop the plantingActionCheck coroutine.
        //Accounts for the case when the player leaves the coral there but did not complete the plantation.
        if (coralValidAtSpot != null && other.CompareTag("Player"))
        {
            InterruptAction();
            StopCoroutine(plantingActionCheck);
            return;
        }
    }
    #endregion

    #region CustomMethods

    //Validate if the coral is appropiate for the spot and displays a visual cue.
    private bool IsTheRightCoral(Coral coral)
    {
        if (coral.coralType != Actioner || !coral.IsPlantationReady)
        {
            PlantingZoneMesh.material = incorrectTypeMaterial;
            return false;
        }
        else
        {
            PlantingZoneMesh.material = correctTypeMaterial;
            return true;
        }
    }

    //The coroutine where the planting-action check is executed. Runs at -coroutineTimeStep- times per second.
    private IEnumerator PlantingActionCheckCoroutine()
    {
        while (true)
        {
            //The execution of the planting action is considered to be in progress only when a coral is inside the planting spot and it is being held. The action can be interrupted and restore at a later time.
            if (coralValidAtSpot != null && coralValidAtSpot.GetComponent<MyGrabable>().IsHeld)
            {
                ExecuteAction();
            }
            else if (coralValidAtSpot == null || !coralValidAtSpot.GetComponent<MyGrabable>().IsHeld)//The planting action is interrupted, if the player stops holding the coral.
            {
                InterruptAction();
            }

            //when the player has maintained the coral held inside the plating spot for enough time, the coroutine ends, and the action is complete.
            if (secondsLeftToCompleteAction < 0)
            {
                CompleteAction();
                break;
            }
            yield return new WaitForSeconds(coroutineTimeStep);
        }
    }

    private void InterruptAction()
    {
        if (audioSource.clip.Equals(plantingSFX)) audioSource.Stop(); //stop audio cuing for the planting action.
        else return;
    }

    private void ExecuteAction()
    {
        secondsLeftToCompleteAction -= 1 * coroutineTimeStep; //update the time as the player is grabbing the coral inside the planting spot.

        //Debug.Log(secondsLeftToCompleteAction.ToString("F1"));

        progressBar.FillAmount = Normalize(secondsToCompleteAction, 0, secondsLeftToCompleteAction);////Update the progress bar to cue how much time is left to complete the action.

        //play sound FX to cue that a planting action is being executed.
        if (!audioSource.isPlaying)
        {
            audioSource.clip = plantingSFX;
            audioSource.Play();
        }
    }

    //Conclude the planting action.
    private void CompleteAction()
    {
        audioSource.clip = actionCompleteSFX;
        audioSource.PlayOneShot(actionCompleteSFX);

        coralValidAtSpot.GetComponent<MyGrabable>().enabled = false;//disable the planted coral as a grabbable object. 

        //Set the transform for the planted coral.
        coralValidAtSpot.GetComponent<Rigidbody>().isKinematic = true;
        coralValidAtSpot.transform.position = CoralAttachPoint.transform.position;
        coralValidAtSpot.transform.rotation = CoralAttachPoint.transform.rotation;

        // Update the corals dictionary amounts based on the currently planted coral.  
        Coral coral = coralValidAtSpot.GetComponent<Coral>();
        CoralPlanting.coralsCollection[coral.coralType] = CoralPlanting.coralsCollection[coral.coralType] + 1;

        //Invoke the OnPlantingActionComplete event passing the updated coral dictionary as parameter.
        OnPlantingActionComplete?.Invoke(CoralPlanting.coralsCollection);

        Destroy(this.gameObject, actionCompleteSFX.length); //Destroy the planting spot game object.
    }

    //Normalize a set of values with a minimum and maximum values between 0 and 1.
    private float Normalize(float min, float max, float value)
    {
        float value_norm = (value - min) / (max - min);
        return value_norm;
    }

    //Populates the planted corals dictionary with all coral types available in the game, and sets each to a 0 amount. 
    public static Dictionary<CoralTypes, int> InitDict()
    {
        Dictionary<CoralTypes, int> temp_coralsCollection = new Dictionary<CoralTypes, int>();

        foreach (CoralTypes val in Enum.GetValues(typeof(CoralTypes)))
        {
            temp_coralsCollection.Add(val, 0);
        }
        return temp_coralsCollection;
    }
    #endregion
}
