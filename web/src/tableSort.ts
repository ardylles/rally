export function compareCells(a:string,b:string){
 const clean=(s:string)=>s.trim().replace(/,/g,"");
 const x=clean(a),y=clean(b);
 const empty=(s:string)=>!s||s==="—"||s==="–";
 if(empty(x)||empty(y))return empty(x)===empty(y)?0:empty(x)?1:-1;
 const numeric=/^[+-]?\d+(\.\d+)?%?$/;
 if(numeric.test(x)&&numeric.test(y))return parseFloat(x)-parseFloat(y);
 return x.localeCompare(y,undefined,{numeric:true,sensitivity:"base"});
}
