using System;
using UnityEngine;

// PreserveAttribute 정의 - Unity.Scripting 어셈블리가 없을때 컴파일 오류 해결
namespace UnityEngine.Scripting
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Constructor, AllowMultiple = false)]
    public sealed class PreserveAttribute : Attribute
    {
        public PreserveAttribute() {}
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireAttributeUsagesAttribute : Attribute
    {
        public RequireAttributeUsagesAttribute() {}
    }
}

// 전역 네임스페이스에도 정의해서 모든 코드에서 접근 가능하게
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Constructor, AllowMultiple = false)]
public sealed class PreserveAttribute : Attribute
{
    public PreserveAttribute() {}
}