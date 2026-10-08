const fs = require('node:fs/promises');
const sharp = require(process.env.SHARP_MODULE || 'sharp');

async function main() {
  const manifest = JSON.parse(await fs.readFile('wwwroot/seed-images/lifestyle-published-images.json','utf8'));
  const entries = Object.entries(manifest).filter(([key]) => key.startsWith('style-wave/') && /-[123]$/.test(key));
  const tiles = [];
  for (let index=0; index<entries.length; index++) {
    const [key,url] = entries[index];
    const response = await fetch(url);
    if (!response.ok) throw new Error(key);
    const image = Buffer.from(await response.arrayBuffer());
    await fs.writeFile(`artifacts/clothing-review-${index+1}.jpg`, image);
    const tile = await sharp(image).resize(180,220,{fit:'contain',background:'#ffffff'}).jpeg().toBuffer();
    tiles.push({input:tile,left:(index%6)*180,top:Math.floor(index/6)*220});
    console.log(`${index+1}: ${key}`);
  }
  await sharp({create:{width:1080,height:Math.ceil(entries.length/6)*220,channels:3,background:'#ffffff'}})
    .composite(tiles).jpeg().toFile('artifacts/clothing-review-sheet.jpg');
}
main().catch(error => { console.error(error.message); process.exitCode=1; });
