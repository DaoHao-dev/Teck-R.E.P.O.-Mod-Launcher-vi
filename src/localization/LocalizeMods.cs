using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Teck.Vietnamese;
class LocalizeMods {
    static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var n in Types(t.NestedTypes))yield return n;}}
    static int Main(string[] args){
        string input=args[0],output=args[1],translator=args[2];Directory.CreateDirectory(output);
        var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(input);resolver.AddSearchDirectory(Path.GetDirectoryName(translator));resolver.AddSearchDirectory(args[3]);
        using(var language=AssemblyDefinition.ReadAssembly(translator)){
            var bridge=language.MainModule.Types.First(t=>t.FullName=="Teck.Vietnamese.UIBridge");var bridgeMethods=bridge.Methods.ToDictionary(m=>m.Name);
            foreach(string file in Directory.GetFiles(input,"*.dll")){
                if(!new[]{"MorePlayers.dll","SharedUpgradesPlus.dll","REPOConfig.dll","MenuLib.dll","Zichen-GameTools-1.0.9.dll","Zichen-CosmeticsTools-1.0.1.dll"}.Contains(Path.GetFileName(file)))continue;
                using(var assembly=AssemblyDefinition.ReadAssembly(file,new ReaderParameters{AssemblyResolver=resolver})){
                    var module=assembly.MainModule;if(assembly.Name.HasPublicKey)throw new Exception("Refusing signed assembly: "+file);
                    var helper=new TypeDefinition("Teck.Vietnamese","DisplayWrappers",TypeAttributes.NotPublic|TypeAttributes.Abstract|TypeAttributes.Sealed,module.TypeSystem.Object);module.Types.Add(helper);int strings=0,calls=0,english=0;
                    var protectedStrings=Types(module.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody&&(m.Name=="BindConfig"||m.Name=="Awake"||m.Name==".cctor")).ToDictionary(m=>m.FullName,m=>String.Join("\n",m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr).Select(i=>(string)i.Operand)));
                    foreach(var type in Types(module.Types).ToArray())foreach(var method in type.Methods.ToArray()){
                        if(type==helper||!method.HasBody)continue;
                        // Expanded short branches cannot overflow after inserting instructions.
                        var longOps=typeof(OpCodes).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Name);
                        foreach(var branch in method.Body.Instructions)if(branch.OpCode.OperandType==OperandType.ShortInlineBrTarget)branch.OpCode=longOps[branch.OpCode.Name.Substring(0,branch.OpCode.Name.Length-2)];
                        if(type.FullName=="Zichen_GameTools.Plugin"&&method.Name=="UseChinese"){
                            method.Body.Instructions.Clear();method.Body.ExceptionHandlers.Clear();method.Body.Variables.Clear();method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));english++;continue;
                        }
                        bool display=method.Name.StartsWith("Draw")||method.Name=="OnGUI"||Path.GetFileName(file)=="REPOConfig.dll"&&method.Name!="Awake"&&method.Name!="FixNaming";
                        foreach(var ins in method.Body.Instructions.ToArray()){
                            // Keep original English literals so switching language works on every redraw.
                            if(display&&ins.OpCode==OpCodes.Ldstr&&!Path.GetFileName(file).StartsWith("Zichen-")){string original=(string)ins.Operand,value=TextMap.Translate(original);if(original!=value){ins.Operand=value;strings++;}}
                            if(type.FullName=="Zichen_CosmeticsTools.Plugin"&&ins.OpCode==OpCodes.Ldfld&&((FieldReference)ins.Operand).Name=="_useChinese"&&method.Name!="BindConfig"){
                                ins.OpCode=OpCodes.Pop;ins.Operand=null;method.Body.GetILProcessor().InsertAfter(ins,Instruction.Create(OpCodes.Ldc_I4_0));english++;
                            }
                            if(ins.OpCode!=OpCodes.Call&&ins.OpCode!=OpCodes.Callvirt)continue;
                            var target=ins.Operand as MethodReference;if(target==null||target.HasGenericParameters)continue;
                            bool gui=(target.DeclaringType.FullName=="UnityEngine.GUI"||target.DeclaringType.FullName=="UnityEngine.GUILayout")&&new[]{"Label","Button","Toggle","Box","Window","ModalWindow","Toolbar","SelectionGrid","RepeatButton"}.Contains(target.Name);
                            bool tmp=target.DeclaringType.FullName.StartsWith("TMPro.")&&(target.Name=="set_text"||target.Name=="SetText");
                            if(!gui&&!tmp||!target.Parameters.Any(p=>new[]{"System.String","System.String[]","UnityEngine.GUIContent","UnityEngine.GUIContent[]"}.Contains(p.ParameterType.FullName)))continue;
                            var wrapper=new MethodDefinition("Render_"+calls,MethodAttributes.Public|MethodAttributes.Static,module.ImportReference(target.ReturnType));helper.Methods.Add(wrapper);
                            if(target.HasThis)wrapper.Parameters.Add(new ParameterDefinition("instance",ParameterAttributes.None,module.ImportReference(target.DeclaringType)));
                            foreach(var p in target.Parameters)wrapper.Parameters.Add(new ParameterDefinition(p.Name,p.Attributes,module.ImportReference(p.ParameterType)));
                            wrapper.Body.MaxStackSize=Math.Max(8,wrapper.Parameters.Count+1);var il=wrapper.Body.GetILProcessor();int index=0;
                            foreach(var p in wrapper.Parameters){il.Append(Instruction.Create(OpCodes.Ldarg,p));
                                if(tmp&&target.HasThis&&index==0){il.Append(Instruction.Create(OpCodes.Dup));il.Append(Instruction.Create(OpCodes.Call,module.ImportReference(bridgeMethods["PrepareTMP"])));}
                                if(!target.HasThis||index!=0){string name=null;switch(p.ParameterType.FullName){case "System.String":name="Text";break;case "System.String[]":name="Texts";break;case "UnityEngine.GUIContent":name="Content";break;case "UnityEngine.GUIContent[]":name="Contents";break;case "UnityEngine.GUIStyle":name="Style";break;}if(name!=null)il.Append(Instruction.Create(OpCodes.Call,module.ImportReference(bridgeMethods[name])));}index++;
                            }
                            il.Append(Instruction.Create(ins.OpCode,module.ImportReference(target)));
                            if(gui&&target.Name=="Button"&&target.ReturnType.FullName=="System.Boolean"){
                                var label=wrapper.Parameters.FirstOrDefault(p=>p.ParameterType.FullName=="System.String");
                                if(label!=null){il.Append(Instruction.Create(OpCodes.Ldarg,label));il.Append(Instruction.Create(OpCodes.Call,module.ImportReference(bridgeMethods["LanguageButton"])));}
                            }
                            il.Append(Instruction.Create(OpCodes.Ret));ins.OpCode=OpCodes.Call;ins.Operand=wrapper;calls++;
                        }
                        if(Path.GetFileName(file)=="REPOConfig.dll"&&method.Name=="FixNaming"&&method.ReturnType.FullName=="System.String")foreach(var ret in method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){
                            ret.OpCode=OpCodes.Call;ret.Operand=module.ImportReference(bridgeMethods["Text"]);method.Body.GetILProcessor().InsertAfter(ret,Instruction.Create(OpCodes.Ret));
                        }
                    }
                    string outFile=Path.Combine(output,Path.GetFileName(file));assembly.Write(outFile);
                    using(var verify=AssemblyDefinition.ReadAssembly(outFile,new ReaderParameters{AssemblyResolver=resolver})){
                        if(!Types(verify.MainModule.Types).SelectMany(t=>t.Methods).Any(m=>m.DeclaringType.FullName=="Teck.Vietnamese.DisplayWrappers")&&calls>0)throw new Exception("Missing wrappers");
                        foreach(var wrapper in verify.MainModule.Types.First(t=>t.FullName=="Teck.Vietnamese.DisplayWrappers").Methods){
                            int stack=0;
                            foreach(var op in wrapper.Body.Instructions){
                                if(op.OpCode==OpCodes.Ldarg)stack++;
                                else if(op.OpCode==OpCodes.Dup)stack++;
                                else if(op.OpCode==OpCodes.Call||op.OpCode==OpCodes.Callvirt){
                                    var reference=(MethodReference)op.Operand;if(reference.Resolve()==null)throw new Exception("Unresolved call: "+reference.FullName);
                                    stack-=reference.Parameters.Count+(reference.HasThis?1:0);if(stack<0)throw new Exception("Invalid argument stack: "+wrapper.FullName);
                                    if(reference.ReturnType.FullName!="System.Void")stack++;
                                }else if(op.OpCode==OpCodes.Ret){stack-=wrapper.ReturnType.FullName=="System.Void"?0:1;if(stack!=0)throw new Exception("Invalid return stack: "+wrapper.FullName);}
                                else throw new Exception("Unexpected wrapper opcode: "+op);
                            }
                        }
                        foreach(var m in Types(verify.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody&&protectedStrings.ContainsKey(m.FullName))){
                            string current=String.Join("\n",m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr).Select(i=>(string)i.Operand));if(current!=protectedStrings[m.FullName])throw new Exception("Config/identifier strings changed: "+m.FullName);
                        }
                        Console.WriteLine(Path.GetFileName(file)+" | "+strings+" literal edits | "+calls+" explicit UI calls | "+english+" language paths");
                    }
                }
            }
        }return 0;
    }
}
