﻿//  Copyright 2004-2012 Castle Project - http://www.castleproject.org/
//  Hamilton Verissimo de Oliveira and individual contributors as indicated. 
//  See the committers.txt/contributors.txt in the distribution for a 
//  full listing of individual contributors.
// 
//  This is free software; you can redistribute it and/or modify it
//  under the terms of the GNU Lesser General Public License as
//  published by the Free Software Foundation; either version 3 of
//  the License, or (at your option) any later version.
// 
//  You should have received a copy of the GNU Lesser General Public
//  License along with this software; if not, write to the Free
//  Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
//  02110-1301 USA, or see the FSF site: http://www.fsf.org.

namespace Castle.MonoRail.Extension.OData

open System
open System.Collections
open System.Collections.Specialized
open System.Collections.Generic
open System.Data.OData
open System.Data.Services.Providers
open System.Linq
open System.Linq.Expressions
open System.Text
open System.Reflection
open System.Web
open Castle.MonoRail
open FParsec
open FParsec.Primitives
open FParsec.CharParsers


type QueryAst = 
    | Element
    | Null
    | Literal of Type * obj
    | PropertyAccess of QueryAst * PropertyInfo
    | BinaryExp of QueryAst * QueryAst * BinaryOp
    | UnaryExp of QueryAst * UnaryOp

module QuerySemanticAnalysis =
    begin
        
        let analyze_and_convert (exp:Exp) (rt:ResourceType) : QueryAst = 
            
            let rec r_analyze e (rt:ResourceType) = 
                match e with 
                | Exp.Element          -> QueryAst.Element, rt

                | Exp.Literal (edm, v) ->
                    let literal = 
                        match edm with
                        | EdmPrimitives.Null      -> QueryAst.Null 
                        | EdmPrimitives.SString   -> QueryAst.Literal (typeof<string>, v)
                        | EdmPrimitives.Int16     -> QueryAst.Literal (typeof<int16>, Convert.ToInt16(v))
                        | EdmPrimitives.Int32     -> QueryAst.Literal (typeof<int32>, Convert.ToInt32(v))
                        | EdmPrimitives.Int64     -> QueryAst.Literal (typeof<int64>, Convert.ToInt64(v))
                        | EdmPrimitives.Single    -> QueryAst.Literal (typeof<float32>, Convert.ToSingle(v))
                        | EdmPrimitives.Decimal   -> QueryAst.Literal (typeof<decimal>, Convert.ToDecimal(v))
                        | EdmPrimitives.Double    -> QueryAst.Literal (typeof<double>, Convert.ToDouble(v))
                        | EdmPrimitives.DateTime  -> QueryAst.Literal (typeof<DateTime>, DateTime.Parse(v))
                        | EdmPrimitives.Boolean   -> QueryAst.Literal (typeof<bool>, Convert.ToBoolean(v))
                        | EdmPrimitives.Guid      -> QueryAst.Literal (typeof<Guid>, Guid.Parse(v))
                        | _ -> failwithf "Unsupported edm primitive type %O" edm
                    
                    // not sure about this one. shouldn't we return the rt for the literal type?
                    literal, rt

                | Exp.MemberAccess (ex, id) ->
                    let name = 
                        match id with 
                        | Identifier i -> i
                        | _ -> failwith "Only Identifier nodes are supported as the rhs of a MemberAccess node"

                    let get_prop (name:string) (rt:ResourceType) = 
                        match rt.Properties |> Seq.tryFind (fun p -> p.Name === name) with
                        | Some p -> p
                        | _ -> failwith "Property not found?"                    

                    let root, nestedRt = r_analyze ex rt

                    // rt.InstanceType.GetProperty(p.Name, BindingFlags.Public ||| BindingFlags.Instance)
                    let prop = get_prop name nestedRt
                    let propInfo = nestedRt.InstanceType.GetProperty(prop.Name, BindingFlags.Public ||| BindingFlags.Instance)

                    QueryAst.PropertyAccess(root, propInfo), prop.ResourceType



                | Exp.Binary (ex1, op, ex2) ->
                    
                    let texp1, _ = r_analyze ex1 rt
                    let texp2, _ = r_analyze ex2 rt

                    QueryAst.BinaryExp(texp1, texp2, op), rt



                | Exp.Unary (op, exp) ->

                    let exp1, _ = r_analyze exp rt

                    QueryAst.UnaryExp(exp1, op), rt

                | _ -> failwithf "Unsupported exp type %O" e

            let newTree, _ = r_analyze exp rt
            newTree

    end

