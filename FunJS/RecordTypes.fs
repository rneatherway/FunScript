﻿module internal FunJS.RecordTypes

open AST
open Microsoft.FSharp.Quotations
open System.Reflection

let private getRecordVars recType =
   Objects.getFields recType
   |> Seq.map fst
   |> Seq.map (fun name -> Var(name, typeof<obj>))
   |> Seq.toList

let private createConstructor recType compiler =
   let vars = getRecordVars recType
   vars, Block [  
      for var in vars do yield Assign(PropertyGet(This, var.Name), Reference var)
      yield! compiler |> Objects.genInstanceMethods recType
   ]

let private creation =
   CompilerComponent.create <| fun (|Split|) compiler returnStategy ->
      function
      | Patterns.NewRecord(recType, exprs) when recType.Name = typeof<Ref<obj>>.Name ->
         let decls, refs = 
            exprs 
            |> List.map (fun (Split(valDecl, valRef)) -> valDecl, valRef)
            |> List.unzip
         let propNames = getRecordVars recType |> List.map (fun v -> v.Name)
         let fields = List.zip propNames refs
         [  yield! decls |> Seq.concat 
            yield returnStategy.Return <| Object fields
         ]
      | Patterns.NewRecord(recType, exprs) ->
         let decls, refs = 
            exprs 
            |> List.map (fun (Split(valDecl, valRef)) -> valDecl, valRef)
            |> List.unzip
         let ci = 
            recType.GetConstructors(
               BindingFlags.Public ||| 
               BindingFlags.NonPublic ||| 
               BindingFlags.Instance).[0]
         let name = JavaScriptNameMapper.mapMethod ci
         let cons = compiler.DefineGlobal name (fun () -> createConstructor recType compiler)
         [ yield! decls |> Seq.concat 
           yield returnStategy.Return <| New(Reference(cons), refs)
         ]
      | _ -> []

let components = [ creation ]