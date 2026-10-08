const fs = require('node:fs/promises');
const path = require('node:path');

const clothing = [
  ['basic-cotton-tee', 'classic-organic-tee-t-shirt-oyster-grey-male', 'تيشيرت قطن كلاسيكي', 'بيج فاتح'],
  ['oversized-graphic-tee', 'oversized-organic-t-shirt-faded-indigo-male', 'تيشيرت قطن واسع', 'أزرق نيلي'],
  ['ribbed-long-sleeve', 'oversized-organic-ls-t-shirt-faded-indigo-male', 'تيشيرت طويل الأكمام', 'أزرق نيلي'],
  ['linen-summer-shirt', 'heavy-organic-boxy-tee-deep-black-male', 'تيشيرت قطن ثقيل', 'أسود'],
  ['premium-zip-hoodie', 'organic-oversized-hood-oyster-grey-male', 'هودي قطن واسع', 'بيج فاتح'],
  ['cropped-denim-jacket', 'organic-oversized-crew-faded-khaki-male', 'سويت شيرت واسع', 'كاكي'],
  ['puffer-light-jacket', 'classic-organic-hood-faded-indigo-male', 'هودي قطن كلاسيكي', 'أزرق نيلي'],
  ['wide-leg-jeans', 'organic-relaxed-sweatpants-faded-khaki-male', 'بنطال رياضي مريح', 'كاكي'],
  ['tailored-straight-pants', 'organic-straight-leg-sweatpants-faded-red-male', 'بنطال رياضي مستقيم', 'أحمر هادئ'],
  ['pleated-midi-skirt', 'organic-straight-leg-sweatpants-faded-khaki-male', 'بنطال قطن مستقيم', 'كاكي'],
  ['casual-knit-dress', 'organic-sweatpants-faded-indigo-male', 'بنطال قطن نيلي', 'أزرق نيلي'],
  ['athletic-jogger-set', 'organic-oversized-crew-oyster-grey-male', 'سويت شيرت قطن', 'بيج فاتح']
];
const cups = [
  ['classic-ceramic-mug', '21178', 'كوب سيراميك MUI', '260ml'],
  ['matte-latte-mug', '21182', 'كوب وصحن MUI', '300ml'],
  ['espresso-cup-set', '21180', 'كوب Espresso مع صحن', '90ml'],
  ['glass-tea-cup', '8437', 'كوب شاي زجاجي CAST', '220ml'],
  ['insulated-travel-tumbler', '20441', 'كوب سفر TO GO بغطاء', '360ml'],
  ['stainless-steel-tumbler', '20211', 'كوب حراري TRAIL ستانلس ستيل', '580ml'],
  ['sport-water-cup', '20151', 'كوب ماء WATER TUMBLER', '550ml'],
  ['minimal-desk-mug', '21532', 'كوب زجاجي LUX للمكتب', '360ml'],
  ['heart-gift-mug', '21517', 'كوب زجاجي LUX للهدايا', '280ml'],
  ['custom-name-mug', '8330', 'كوب وصحن زجاجي UNITEA', '230ml'],
  ['speckled-handmade-mug', '21889', 'كوب قهوة سيراميك CLK', '180ml'],
  ['kids-color-cup', '26450', 'كوب أطفال BONBO بقشة', '260ml']
];
const colors = { black:'أسود', white:'أبيض', cream:'كريمي', brown:'بني', clear:'شفاف', smoke:'دخاني', amber:'عنبر', 'sand beige':'بيج رملي', 'ice gray':'رمادي ثلجي', 'ash gray':'رمادي', 'blue gray':'أزرق رمادي', pink:'وردي', navy:'كحلي', maroon:'خمري', 'deep green':'أخضر غامق', 'deep navy':'كحلي غامق', 'khaki':'كاكي', 'natural':'طبيعي', 'off white':'أبيض عاجي' };
const colorName = color => colors[String(color).toLowerCase()] || color;
const normalizedUrl = url => { const parsed = new URL(url); parsed.searchParams.set('width','1200'); parsed.searchParams.set('format','jpg'); return parsed.href; };

async function catalog(url) {
  const response = await fetch(url, { signal: AbortSignal.timeout(30000) });
  if (!response.ok) throw new Error(`Catalog ${response.status}`);
  return (await response.json()).products;
}

async function main() {
  const apparel = await catalog('https://colorfulstandard.com/products.json?limit=250');
  const drinkware = await catalog('https://www.kinto-usa.com/products.json?limit=250');
  const images = {};
  const overrides = {};
  for (const [slug, definitions, source, host] of [
    ['style-wave', clothing, apparel, 'https://colorfulstandard.com'],
    ['mug-mood', cups, drinkware, 'https://www.kinto-usa.com']
  ]) {
    for (const [key, handle, name, capacity] of definitions) {
      const product = source.find(item => item.handle === handle);
      if (!product || product.images.length < 3) throw new Error(`Missing photos: ${handle}`);
      const variants = product.variants.map(variant => ({ ...variant,
        featured_image: variant.featured_image || product.variants.find(other => other.option1 === variant.option1 && other.featured_image)?.featured_image
          || (variant.option1 === product.variants[0].option1 ? product.images[0] : null)
      }));
      let selected;
      let primaryPhoto;
      if (slug === 'style-wave') {
        if (!handle.endsWith('-male') || handle.includes('female')) throw new Error(`Unreviewed apparel source: ${handle}`);
        const sizeIndex = product.options.findIndex(option => option.name.toLowerCase() === 'size') + 1;
        selected = [...new Map(variants.filter(variant => variant[`option${sizeIndex}`]).map(variant => [variant[`option${sizeIndex}`],variant])).values()].slice(0,4);
        if (selected.length < 2) throw new Error(`Missing clothing sizes: ${handle}`);
        const picturedColor = capacity;
        primaryPhoto = product.images[0];
        overrides[`${slug}/${key}`] = {
          Name:name, Description:`${name}، بالمقاسات والألوان الموضحة في الخيارات.`,
          VariantNames:selected.map(variant => `${picturedColor} - ${variant[`option${sizeIndex}`]}`),
          VariantImageUrls:selected.map(() => normalizedUrl(primaryPhoto.src)),
          SourceUrl:`${host}/products/${handle}`
        };
      } else {
        selected = variants.filter(variant => variant.featured_image?.src).slice(0,3);
        if (!selected.length) throw new Error(`Missing cup colors: ${handle}`);
        overrides[`${slug}/${key}`] = {
          Name:name, Description:`${name} بسعة ${capacity}، بالألوان الموضحة في الخيارات.`,
          VariantNames:selected.map(variant => `${colorName(variant.option1)} - ${capacity}`),
          VariantImageUrls:selected.map(variant => normalizedUrl(variant.featured_image.src)),
          SourceUrl:`${host}/products/${handle}`
        };
      }
      const gallery = [...new Set([...(primaryPhoto ? [primaryPhoto.src] : selected.map(variant => variant.featured_image.src)), ...product.images.map(image => image.src)])].slice(0,3);
      if (slug === 'style-wave' && gallery.some(url => /female|women/i.test(new URL(url).pathname)))
        throw new Error(`Unreviewed gallery photo: ${handle}`);
      gallery.forEach((url,index) => { images[`${slug}/${key}-${index+1}`] = normalizedUrl(url); });
    }
  }
  for (const [slug, first, categoryKeys] of [
    ['style-wave','basic-cotton-tee',{tops:'basic-cotton-tee',hoodies:'premium-zip-hoodie',bottoms:'wide-leg-jeans'}],
    ['mug-mood','classic-ceramic-mug',{coffee:'classic-ceramic-mug',travel:'insulated-travel-tumbler',gift:'heart-gift-mug'}]
  ]) {
    images[`${slug}/store-logo`] = images[`${slug}/${first}-1`];
    images[`${slug}/store-cover`] = images[`${slug}/${first}-3`];
    for (const [category,product] of Object.entries(categoryKeys)) images[`${slug}/category-${category}`] = images[`${slug}/${product}-1`];
  }
  const urls = [...new Set([...Object.values(images), ...Object.values(overrides).flatMap(item => item.VariantImageUrls)])];
  for (let start=0; start<urls.length; start+=8) {
    await Promise.all(urls.slice(start,start+8).map(async url => {
      const response = await fetch(url,{method:'HEAD',signal:AbortSignal.timeout(30000)});
      if (!response.ok || !response.headers.get('content-type')?.startsWith('image/')) throw new Error(`Broken photo: ${url}`);
    }));
  }
  const directory = path.resolve('wwwroot/seed-images');
  await fs.writeFile(path.join(directory,'lifestyle-published-images.json'),JSON.stringify(images,null,2)+'\n');
  await fs.writeFile(path.join(directory,'lifestyle-photo-catalog.json'),JSON.stringify(overrides,null,2)+'\n');
  console.log(`Verified ${urls.length} real photo URLs for ${Object.keys(overrides).length} products.`);
}
main().catch(error => { console.error(error.message); process.exitCode=1; });
