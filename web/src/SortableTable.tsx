import {Children,cloneElement,isValidElement,useRef,useState} from "react";
import type {ReactNode,ReactElement,HTMLAttributes} from "react";
type Element = ReactElement<{children?:ReactNode;[key:string]:unknown}>;
import {compareCells} from "./tableSort";
function cellValue(cell:HTMLTableCellElement|undefined){
 if(!cell)return "";
 const input=cell.querySelector("input,select") as HTMLInputElement|HTMLSelectElement|null;
 if(input instanceof HTMLSelectElement)return input.selectedOptions[0]?.text??"";
 if(input instanceof HTMLInputElement)return input.type==="checkbox"?(input.checked?"Yes":"No"):input.value;
 return cell.textContent??"";
}
export function SortableTable({children,...props}:HTMLAttributes<HTMLTableElement>){
 const ref=useRef<HTMLTableElement>(null);
 const [sort,setSort]=useState<{column:number;descending:boolean;orders:string[][]}|null>(null);
 function toggle(column:number){
  const descending=sort?.column===column?!sort.descending:false;
  const orders=Array.from(ref.current?.tBodies??[]).map(body=>Array.from(body.rows).map((row,index)=>({key:row.dataset.sortKey!,value:cellValue(row.cells[column]),index})).sort((a,b)=>{
   const missing=(s:string)=>!s.trim()||["—","–"].includes(s.trim());
   if(missing(a.value)!==missing(b.value))return missing(a.value)?1:-1;
   return compareCells(a.value,b.value)*(descending?-1:1)||a.index-b.index;
  }).map(row=>row.key));
  setSort({column,descending,orders});
 }
 let bodyIndex=0;
 return <table {...props} ref={ref}>{Children.map(children,child=>{
  if(!isValidElement(child))return child;
  const section=child as Element;
  if(section.type==="thead")return cloneElement(section,{},Children.map(section.props.children,row=>{
   if(!isValidElement(row))return row;
   const tr=row as Element;
   return cloneElement(tr,{},Children.map(tr.props.children,(header,column)=>{
    if(!isValidElement(header))return header;
    const th=header as Element;
    if(!th.props.children||["actions","action",""].includes(String(th.props.children).trim().toLowerCase()))return th;
    return cloneElement(th,{"aria-sort":sort?.column===column?(sort.descending?"descending":"ascending"):"none"},<button type="button" className="sort-heading" onClick={()=>toggle(column)}>{th.props.children}<span aria-hidden="true">{sort?.column===column?(sort.descending?" ↓":" ↑"):" ↕"}</span></button>);
   }));
  }));
  if(section.type==="tbody"){
   const rows=Children.toArray(section.props.children);
   const order=sort?.orders[bodyIndex++];
   const keyed=rows.map((row,index)=>({row,key:isValidElement(row)&&row.key!=null?String(row.key):String(index),index}));
   if(order){const positions=new Map(order.map((key,i)=>[key,i]));keyed.sort((a,b)=>(positions.get(a.key)??order.length+a.index)-(positions.get(b.key)??order.length+b.index));}
   return cloneElement(section,{},keyed.map(({row,key})=>isValidElement(row)?cloneElement(row as Element,{key,"data-sort-key":key}):row));
  }
  return child;
 })}</table>;
}
