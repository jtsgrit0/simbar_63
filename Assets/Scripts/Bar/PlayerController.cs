using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float rotateSpeed = 10f;

    private CharacterController controller;
    private Vector3 inputVector;
    private Animator animator;
    private Animation walkingAnimation;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        walkingAnimation = GetComponent<Animation>();

        // The project uses the legacy Animation component for Walking.fbx.
        // An Animator with no controller can prevent that state from updating.
        if (animator != null && animator.runtimeAnimatorController == null)
        {
            animator.enabled = false;
        }

        if (walkingAnimation != null && walkingAnimation.clip != null)
        {
            walkingAnimation.clip.wrapMode = WrapMode.Loop;
            walkingAnimation.wrapMode = WrapMode.Loop;
            walkingAnimation.playAutomatically = true;
            walkingAnimation.Play();
        }
    }

    private void Update()
    {
        if (controller == null) return;

        Vector2 moveInput = ReadMovementInput();
        float h = moveInput.x;
        float v = moveInput.y;

        Vector3 forward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (right * h + forward * v).normalized;
        controller.Move(move * moveSpeed * Time.deltaTime);

        if (move.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        }

        if (ReadInteractPressed())
        {
            TryInteract();
        }
    }

    private Vector2 ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float vertical = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        return new Vector2(horizontal, vertical);
    }

    private bool ReadInteractPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        return keyboard.eKey.wasPressedThisFrame;
    }

    private void TryInteract()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        if (Physics.Raycast(ray, out RaycastHit hit, 3f))
        {
            if (hit.collider.TryGetComponent(out BarCounter bar))
            {
                bar.TryServeCustomer();
            }
        }
    }
}